using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Technologies.Handlers;

public record UpsertTechnologiesRequest(IReadOnlyCollection<UpsertTechnologyDto> Technologies) : IRequest<ServiceResultBase>;

/// <summary>
/// Creates or updates technologies and their costs, prerequisites, grants and experience mappings, matching on Code.
/// </summary>
/// <remarks>
/// <para>
/// The same path the seed and the Entity Setup Tool both take, so seeding and hand-editing cannot diverge in how a row is matched or written.
/// </para>
/// <para>
/// Derive, then validate, then write - the whole request is checked before anything is touched, so a bad entry anywhere fails the lot rather than leaving
/// half a catalogue behind. Two checks here cannot be delegated to the database: that the prerequisite graph is <b>acyclic</b>, and that every grant target
/// and cost Asset <b>resolves</b>, since a grant's target carries no foreign key.
/// </para>
/// <para>
/// Child collections are replaced wholesale, but by <b>keyed upsert</b> rather than delete-and-reinsert: deleting and re-adding the same primary key in one
/// <c>SaveChanges</c> is not reliably ordered, and the keyed form also leaves untouched rows alone. Costs, grants and experience mappings are compared in
/// id-space because their targets must already exist; prerequisites are compared in code-space, because a prerequisite may point at a Technology being
/// created by this same request and so have no id yet.
/// </para>
/// </remarks>
internal class UpsertTechnologiesHandler(
    ILogger<UpsertTechnologiesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpsertTechnologiesRequest, ServiceResultBase>
{
    private const int _SORT_ORDER_STEP = 10;

    public async Task<ServiceResultBase> Handle(
        UpsertTechnologiesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Technologies.Count == 0)
        {
            return Fail("At least one technology must be provided.");
        }

        // --- Step 1: Derive sort orders before touching the database. ---
        // Numbered within the era rather than across the whole request, because the uniqueness constraint is on (Era, SortOrder).
        var plan = request.Technologies
            .GroupBy(dto => dto.Era)
            .SelectMany(era => era.Select((dto, index) => new TechnologyPlan(dto, dto.SortOrder ?? (index + 1) * _SORT_ORDER_STEP)))
            .ToArray();

        List<string> errors = [];

        ValidateShape(plan, errors);

        if (errors.Count > 0)
        {
            return Fail(errors);
        }

        // --- Step 2: Load everything the request references, in bulk. ---
        string[] requestCodes = [.. plan.Select(item => item.Dto.Code)];

        Dictionary<string, Technology> existingTechnologies = await context.Technologies
            .Where(technology => requestCodes.Contains(technology.Code))
            .ToDictionaryAsync(technology => technology.Code, cancellationToken);

        // Prerequisites may name a Technology the request does not mention, so those have to be looked up too.
        string[] prerequisiteCodes = [.. plan
            .SelectMany(item => item.Dto.Prerequisites)
            .Select(prerequisite => prerequisite.PrerequisiteTechnologyCode)
            .Distinct()
            .Where(code => !existingTechnologies.ContainsKey(code))];

        Dictionary<string, Technology> referencedTechnologies = prerequisiteCodes.Length > 0
            ? await context.Technologies
                .Where(technology => prerequisiteCodes.Contains(technology.Code))
                .ToDictionaryAsync(technology => technology.Code, cancellationToken)
            : [];

        ReferenceLookups lookups = await LoadReferenceLookupsAsync(plan, cancellationToken);

        // --- Step 3: Resolve every reference, collecting what cannot be resolved. ---
        // A code in the request that is neither already in the database nor defined by this request has nothing to point at.
        HashSet<string> knownTechnologyCodes =
        [
            .. requestCodes,
            .. existingTechnologies.Keys,
            .. referencedTechnologies.Keys
        ];

        ValidateReferences(plan, knownTechnologyCodes, lookups, errors);

        if (errors.Count > 0)
        {
            return Fail(errors);
        }

        // --- Step 4: Reject cycles. The database cannot express this. ---
        // A cycle makes every Technology in it permanently unreachable, and the availability reconcile never settles. Checked in code-space so that
        // technologies being created by this request count as nodes, and against the graph as it will be once the request is applied - the request
        // replaces a Technology's own edges, and leaves everything else's alone.
        string[] cycle = await FindPrerequisiteCycleAsync(plan, cancellationToken);

        if (cycle.Length > 0)
        {
            return Fail($"Prerequisites would form a cycle: {string.Join(" -> ", cycle)}");
        }

        // --- Step 5: Upsert the technologies themselves. ---
        foreach (TechnologyPlan item in plan)
        {
            if (!existingTechnologies.TryGetValue(item.Dto.Code, out Technology? entity))
            {
                entity = new Technology
                {
                    Code = item.Dto.Code,
                    DisplayName = item.Dto.DisplayName,
                    Description = item.Dto.Description,
                    IconPath = item.Dto.IconPath ?? string.Empty,
                    Era = item.Dto.Era,
                    SortOrder = item.SortOrder
                };

                context.Technologies.Add(entity);
                existingTechnologies[item.Dto.Code] = entity;
            }
            else
            {
                entity.DisplayName = item.Dto.DisplayName;
                entity.Era = item.Dto.Era;
                entity.SortOrder = item.SortOrder;

                // IconPath and Description are only overwritten when supplied, to allow partial updates.
                if (item.Dto.IconPath is not null)
                {
                    entity.IconPath = item.Dto.IconPath;
                }

                if (item.Dto.Description is not null)
                {
                    entity.Description = item.Dto.Description;
                }
            }

            entity.TreeColumn = item.Dto.TreeColumn;
            entity.TreeRow = item.Dto.TreeRow;
            entity.DirectedRequired = item.Dto.DirectedRequired;
            entity.ExperienceRequired = item.Dto.ExperienceRequired;
            entity.DiffusionRequired = item.Dto.DiffusionRequired;
        }

        // --- Step 6: Replace the child collections by keyed upsert. ---
        int[] touchedTechnologyIds = [.. plan
            .Select(item => existingTechnologies[item.Dto.Code].Id)
            .Where(id => id != 0)];

        await SyncAcquisitionCostsAsync(plan, existingTechnologies, lookups, touchedTechnologyIds, cancellationToken);
        await SyncGrantsAsync(plan, existingTechnologies, lookups, touchedTechnologyIds, cancellationToken);
        await SyncExperienceTypesAsync(plan, existingTechnologies, lookups, touchedTechnologyIds, cancellationToken);
        await SyncPrerequisitesAsync(plan, existingTechnologies, referencedTechnologies, touchedTechnologyIds, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Upserted {TechnologyCount} technologies, {CostCount} acquisition costs, {PrerequisiteCount} prerequisites, {GrantCount} grants, "
            + "{ExperienceCount} experience mappings.",
            plan.Length,
            plan.Sum(item => item.Dto.AcquisitionCosts.Count),
            plan.Sum(item => item.Dto.Prerequisites.Count),
            plan.Sum(item => item.Dto.Grants.Count),
            plan.Sum(item => item.Dto.ExperienceTypeCodes.Count));

        return SuccessNoContent();
    }

    /// <summary>
    /// Checks everything that can be judged from the request alone, before any database work.
    /// </summary>
    private static void ValidateShape(TechnologyPlan[] plan, List<string> errors)
    {
        string[] blankCodes = [.. plan
            .Where(item => string.IsNullOrWhiteSpace(item.Dto.Code))
            .Select(item => item.Dto.DisplayName)];

        if (blankCodes.Length > 0)
        {
            errors.Add($"Technology code is required, and is missing for: {string.Join(", ", blankCodes)}");
        }

        string[] duplicateCodes = [.. plan
            .GroupBy(item => item.Dto.Code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateCodes.Length > 0)
        {
            errors.Add($"Duplicate technology code(s): {string.Join(", ", duplicateCodes)}");
        }

        string[] duplicateDisplayNames = [.. plan
            .GroupBy(item => item.Dto.DisplayName)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateDisplayNames.Length > 0)
        {
            errors.Add($"Duplicate technology display name(s): {string.Join(", ", duplicateDisplayNames)}");
        }

        string[] duplicateEraOrders = [.. plan
            .GroupBy(item => new { item.Dto.Era, item.SortOrder })
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Era} {group.Key.SortOrder}")];

        if (duplicateEraOrders.Length > 0)
        {
            errors.Add($"Duplicate era and sort order combination(s): {string.Join(", ", duplicateEraOrders)}");
        }

        // Mirrors CK_Technology_AtLeastOneBucket. Caught here so the message names the technology rather than surfacing as a constraint violation.
        string[] bucketlessCodes = [.. plan
            .Where(item => item.Dto.DirectedRequired is null
                && item.Dto.ExperienceRequired is null
                && item.Dto.DiffusionRequired is null)
            .Select(item => item.Dto.Code)];

        if (bucketlessCodes.Length > 0)
        {
            errors.Add($"Technology must require at least one research bucket: {string.Join(", ", bucketlessCodes)}");
        }

        string[] nonPositiveBuckets = [.. plan
            .Where(item => item.Dto.DirectedRequired <= 0 || item.Dto.ExperienceRequired <= 0 || item.Dto.DiffusionRequired <= 0)
            .Select(item => item.Dto.Code)];

        if (nonPositiveBuckets.Length > 0)
        {
            errors.Add($"A bucket requirement must be greater than zero where it is used, or null where it is not: {string.Join(", ", nonPositiveBuckets)}");
        }

        // An experience requirement with no sources could never be filled, and sources on a technology with no experience bucket have nowhere to go. Both
        // are authoring mistakes rather than states worth supporting.
        string[] unsourcedExperience = [.. plan
            .Where(item => item.Dto.ExperienceRequired is not null && item.Dto.ExperienceTypeCodes.Count == 0)
            .Select(item => item.Dto.Code)];

        if (unsourcedExperience.Length > 0)
        {
            errors.Add($"Technology requires experience but names no experience types to earn it: {string.Join(", ", unsourcedExperience)}");
        }

        string[] strandedExperience = [.. plan
            .Where(item => item.Dto.ExperienceRequired is null && item.Dto.ExperienceTypeCodes.Count > 0)
            .Select(item => item.Dto.Code)];

        if (strandedExperience.Length > 0)
        {
            errors.Add($"Technology names experience types but has no experience requirement to spend them on: {string.Join(", ", strandedExperience)}");
        }

        string[] nonPositiveQuantities = [.. plan
            .Where(item => item.Dto.AcquisitionCosts.Any(cost => cost.Quantity <= 0))
            .Select(item => item.Dto.Code)];

        if (nonPositiveQuantities.Length > 0)
        {
            errors.Add($"Acquisition cost quantities must be greater than zero: {string.Join(", ", nonPositiveQuantities)}");
        }

        // Caught before the database, because a composite primary key violation names the key rather than the technology.
        string[] duplicateCosts = [.. plan
            .Where(item => item.Dto.AcquisitionCosts
                .GroupBy(cost => cost.AssetCode)
                .Any(group => group.Count() > 1))
            .Select(item => item.Dto.Code)];

        if (duplicateCosts.Length > 0)
        {
            errors.Add($"Technology names the same cost Asset more than once: {string.Join(", ", duplicateCosts)}");
        }

        string[] duplicatePrerequisites = [.. plan
            .Where(item => item.Dto.Prerequisites
                .GroupBy(prerequisite => prerequisite.PrerequisiteTechnologyCode)
                .Any(group => group.Count() > 1))
            .Select(item => item.Dto.Code)];

        if (duplicatePrerequisites.Length > 0)
        {
            errors.Add($"Technology names the same prerequisite more than once: {string.Join(", ", duplicatePrerequisites)}");
        }

        string[] duplicateGrants = [.. plan
            .Where(item => item.Dto.Grants
                .GroupBy(grant => new { grant.TargetType, grant.TargetCode })
                .Any(group => group.Count() > 1))
            .Select(item => item.Dto.Code)];

        if (duplicateGrants.Length > 0)
        {
            errors.Add($"Technology grants the same target more than once: {string.Join(", ", duplicateGrants)}");
        }

        string[] duplicateExperienceTypes = [.. plan
            .Where(item => item.Dto.ExperienceTypeCodes
                .GroupBy(code => code)
                .Any(group => group.Count() > 1))
            .Select(item => item.Dto.Code)];

        if (duplicateExperienceTypes.Length > 0)
        {
            errors.Add($"Technology names the same experience type more than once: {string.Join(", ", duplicateExperienceTypes)}");
        }

        // Mirrors CK_TechnologyPrerequisite_NotSelf. A self-edge is a cycle of length one, and caught here it names the technology.
        string[] selfPrerequisites = [.. plan
            .Where(item => item.Dto.Prerequisites.Any(prerequisite => prerequisite.PrerequisiteTechnologyCode == item.Dto.Code))
            .Select(item => item.Dto.Code)];

        if (selfPrerequisites.Length > 0)
        {
            errors.Add($"Technology cannot be its own prerequisite: {string.Join(", ", selfPrerequisites)}");
        }
    }

    /// <summary>
    /// Checks that every code the request points at resolves to something.
    /// </summary>
    /// <remarks>
    /// This is the only guard a grant target gets: <c>TechnologyGrant.TargetId</c> carries no foreign key, because it points at a different table for each
    /// target type.
    /// </remarks>
    private static void ValidateReferences(
        TechnologyPlan[] plan,
        HashSet<string> knownTechnologyCodes,
        ReferenceLookups lookups,
        List<string> errors)
    {
        string[] unknownPrerequisites = [.. plan
            .SelectMany(item => item.Dto.Prerequisites)
            .Select(prerequisite => prerequisite.PrerequisiteTechnologyCode)
            .Where(code => !knownTechnologyCodes.Contains(code))
            .Distinct()];

        if (unknownPrerequisites.Length > 0)
        {
            errors.Add($"Unknown prerequisite technology code(s): {string.Join(", ", unknownPrerequisites)}");
        }

        string[] unknownAssets = [.. plan
            .SelectMany(item => item.Dto.AcquisitionCosts)
            .Select(cost => cost.AssetCode)
            .Where(code => !lookups.Assets.ContainsKey(code))
            .Distinct()];

        if (unknownAssets.Length > 0)
        {
            errors.Add($"Unknown cost Asset code(s): {string.Join(", ", unknownAssets)}. The Assets catalogue must be seeded first.");
        }

        string[] unknownExperienceTypes = [.. plan
            .SelectMany(item => item.Dto.ExperienceTypeCodes)
            .Where(code => !lookups.ExperienceTypes.ContainsKey(code))
            .Distinct()];

        if (unknownExperienceTypes.Length > 0)
        {
            errors.Add($"Unknown experience type code(s): {string.Join(", ", unknownExperienceTypes)}. The experience types must be seeded first.");
        }

        string[] unknownGrantTargets = [.. plan
            .SelectMany(item => item.Dto.Grants)
            .Where(grant => lookups.ResolveGrantTarget(grant.TargetType, grant.TargetCode) is null)
            .Select(grant => $"{grant.TargetType} '{grant.TargetCode}'")
            .Distinct()];

        if (unknownGrantTargets.Length > 0)
        {
            errors.Add($"Unknown grant target(s): {string.Join(", ", unknownGrantTargets)}. The catalogue each one belongs to must be seeded first.");
        }
    }

    /// <summary>
    /// Loads the catalogues a grant, cost or experience mapping can point at, narrowed to the codes the request actually names.
    /// </summary>
    private async Task<ReferenceLookups> LoadReferenceLookupsAsync(TechnologyPlan[] plan, CancellationToken cancellationToken)
    {
        string[] assetCodes = [.. plan
            .SelectMany(item => item.Dto.AcquisitionCosts)
            .Select(cost => cost.AssetCode)
            .Concat(GrantCodesFor(plan, TechnologyGrantTargetType.Asset))
            .Distinct()];

        string[] metaCategoryCodes = [.. GrantCodesFor(plan, TechnologyGrantTargetType.AssetMetaCategory)];
        string[] componentCodes = [.. GrantCodesFor(plan, TechnologyGrantTargetType.UnitComponent)];
        string[] templateCodes = [.. GrantCodesFor(plan, TechnologyGrantTargetType.UnitTemplate)];
        string[] mapResourceTypeCodes = [.. GrantCodesFor(plan, TechnologyGrantTargetType.MapResourceType)];

        string[] experienceTypeCodes = [.. plan
            .SelectMany(item => item.Dto.ExperienceTypeCodes)
            .Distinct()];

        return new ReferenceLookups(
            Assets: assetCodes.Length == 0
                ? []
                : await context.Assets
                    .Where(asset => assetCodes.Contains(asset.Code))
                    .ToDictionaryAsync(asset => asset.Code, asset => asset.Id, cancellationToken),
            AssetMetaCategories: metaCategoryCodes.Length == 0
                ? []
                : await context.AssetMetaCategories
                    .Where(metaCategory => metaCategoryCodes.Contains(metaCategory.Code))
                    .ToDictionaryAsync(metaCategory => metaCategory.Code, metaCategory => metaCategory.Id, cancellationToken),
            UnitComponents: componentCodes.Length == 0
                ? []
                : await context.UnitComponents
                    .Where(component => componentCodes.Contains(component.Code))
                    .ToDictionaryAsync(component => component.Code, component => component.Id, cancellationToken),
            // UnitTemplate.Code is nullable, so a template without one simply cannot be named here.
            UnitTemplates: templateCodes.Length == 0
                ? []
                : await context.UnitTemplates
                    .Where(template => template.Code != null && templateCodes.Contains(template.Code))
                    .ToDictionaryAsync(template => template.Code!, template => template.Id, cancellationToken),
            MapResourceTypes: mapResourceTypeCodes.Length == 0
                ? []
                : await context.MapResourceTypes
                    .Where(mapResourceType => mapResourceTypeCodes.Contains(mapResourceType.Code))
                    .ToDictionaryAsync(mapResourceType => mapResourceType.Code, mapResourceType => mapResourceType.Id, cancellationToken),
            ExperienceTypes: experienceTypeCodes.Length == 0
                ? []
                : await context.ExperienceTypes
                    .Where(experienceType => experienceTypeCodes.Contains(experienceType.Code))
                    .ToDictionaryAsync(experienceType => experienceType.Code, experienceType => experienceType.Id, cancellationToken));
    }

    private static IEnumerable<string> GrantCodesFor(TechnologyPlan[] plan, TechnologyGrantTargetType targetType)
    {
        return plan
            .SelectMany(item => item.Dto.Grants)
            .Where(grant => grant.TargetType == targetType)
            .Select(grant => grant.TargetCode)
            .Distinct();
    }

    /// <summary>
    /// Walks the prerequisite graph as it will be once the request is applied, and returns the technologies left in a cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kahn's algorithm: repeatedly take the nodes with no unmet prerequisites. Anything still standing when nothing more can be taken is in, or downstream
    /// of, a cycle - which is exactly what has to be rejected, since both are unreachable.
    /// </para>
    /// <para>
    /// Worked in code-space rather than on ids, because a technology created by this same request has no id yet. The graph is the existing edges of every
    /// technology the request does <i>not</i> mention, plus the request's own edges - the request replaces a technology's prerequisites rather than adding
    /// to them.
    /// </para>
    /// </remarks>
    private async Task<string[]> FindPrerequisiteCycleAsync(TechnologyPlan[] plan, CancellationToken cancellationToken)
    {
        HashSet<string> replacedCodes = [.. plan.Select(item => item.Dto.Code)];

        // The whole edge table, as codes. It is small - a few hundred technologies with a handful of edges each - and the alternative is resolving ids
        // against codes twice over.
        var storedEdges = await context.TechnologyPrerequisites
            .Select(prerequisite => new
            {
                TechnologyCode = prerequisite.Technology.Code,
                PrerequisiteCode = prerequisite.PrerequisiteTechnology.Code
            })
            .ToArrayAsync(cancellationToken);

        Dictionary<string, HashSet<string>> prerequisitesByCode = [];

        foreach (var edge in storedEdges.Where(edge => !replacedCodes.Contains(edge.TechnologyCode)))
        {
            AddEdge(prerequisitesByCode, edge.TechnologyCode, edge.PrerequisiteCode);
        }

        foreach (TechnologyPlan item in plan)
        {
            // Seed the node even with no prerequisites, so a technology the request clears is still counted.
            if (!prerequisitesByCode.ContainsKey(item.Dto.Code))
            {
                prerequisitesByCode[item.Dto.Code] = [];
            }

            foreach (UpsertTechnologyPrerequisiteDto prerequisite in item.Dto.Prerequisites)
            {
                AddEdge(prerequisitesByCode, item.Dto.Code, prerequisite.PrerequisiteTechnologyCode);
            }
        }

        HashSet<string> settled = [];
        bool progressed = true;

        while (progressed)
        {
            progressed = false;

            foreach ((string code, HashSet<string> prerequisites) in prerequisitesByCode)
            {
                if (settled.Contains(code))
                {
                    continue;
                }

                // A prerequisite outside the graph is a technology with no prerequisites of its own, so it counts as settled.
                if (prerequisites.All(prerequisite => settled.Contains(prerequisite) || !prerequisitesByCode.ContainsKey(prerequisite)))
                {
                    settled.Add(code);
                    progressed = true;
                }
            }
        }

        return [.. prerequisitesByCode.Keys.Where(code => !settled.Contains(code)).Order()];
    }

    private static void AddEdge(Dictionary<string, HashSet<string>> prerequisitesByCode, string technologyCode, string prerequisiteCode)
    {
        if (!prerequisitesByCode.TryGetValue(technologyCode, out HashSet<string>? prerequisites))
        {
            prerequisites = [];
            prerequisitesByCode[technologyCode] = prerequisites;
        }

        prerequisites.Add(prerequisiteCode);
    }

    private async Task SyncAcquisitionCostsAsync(
        TechnologyPlan[] plan,
        Dictionary<string, Technology> technologiesByCode,
        ReferenceLookups lookups,
        int[] touchedTechnologyIds,
        CancellationToken cancellationToken)
    {
        List<TechnologyAcquisitionCost> stored = touchedTechnologyIds.Length == 0
            ? []
            : await context.TechnologyAcquisitionCosts
                .Where(cost => touchedTechnologyIds.Contains(cost.TechnologyId))
                .ToListAsync(cancellationToken);

        foreach (TechnologyPlan item in plan)
        {
            Technology technology = technologiesByCode[item.Dto.Code];

            Dictionary<int, int> desired = item.Dto.AcquisitionCosts
                .ToDictionary(cost => lookups.Assets[cost.AssetCode], cost => cost.Quantity);

            List<TechnologyAcquisitionCost> current = [.. stored.Where(cost => cost.TechnologyId == technology.Id && technology.Id != 0)];

            context.TechnologyAcquisitionCosts.RemoveRange(current.Where(cost => !desired.ContainsKey(cost.AssetId)));

            foreach ((int assetId, int quantity) in desired)
            {
                TechnologyAcquisitionCost? existing = current.FirstOrDefault(cost => cost.AssetId == assetId);

                if (existing is null)
                {
                    // Linked through the navigation property so EF resolves the foreign key even for a technology inserted by this same save.
                    technology.AcquisitionCosts.Add(new TechnologyAcquisitionCost
                    {
                        AssetId = assetId,
                        Quantity = quantity
                    });
                }
                else
                {
                    existing.Quantity = quantity;
                }
            }
        }
    }

    private async Task SyncGrantsAsync(
        TechnologyPlan[] plan,
        Dictionary<string, Technology> technologiesByCode,
        ReferenceLookups lookups,
        int[] touchedTechnologyIds,
        CancellationToken cancellationToken)
    {
        List<TechnologyGrant> stored = touchedTechnologyIds.Length == 0
            ? []
            : await context.TechnologyGrants
                .Where(grant => touchedTechnologyIds.Contains(grant.TechnologyId))
                .ToListAsync(cancellationToken);

        foreach (TechnologyPlan item in plan)
        {
            Technology technology = technologiesByCode[item.Dto.Code];

            Dictionary<(TechnologyGrantTargetType TargetType, int TargetId), TechnologyGrantEffect> desired = item.Dto.Grants
                .ToDictionary(
                    grant => (grant.TargetType, lookups.ResolveGrantTarget(grant.TargetType, grant.TargetCode)!.Value),
                    grant => grant.GrantEffect);

            List<TechnologyGrant> current = [.. stored.Where(grant => grant.TechnologyId == technology.Id && technology.Id != 0)];

            context.TechnologyGrants.RemoveRange(current.Where(grant => !desired.ContainsKey((grant.TargetType, grant.TargetId))));

            foreach (((TechnologyGrantTargetType targetType, int targetId), TechnologyGrantEffect effect) in desired)
            {
                TechnologyGrant? existing = current.FirstOrDefault(grant => grant.TargetType == targetType && grant.TargetId == targetId);

                if (existing is null)
                {
                    technology.Grants.Add(new TechnologyGrant
                    {
                        TargetType = targetType,
                        TargetId = targetId,
                        GrantEffect = effect
                    });
                }
                else
                {
                    existing.GrantEffect = effect;
                }
            }
        }
    }

    private async Task SyncExperienceTypesAsync(
        TechnologyPlan[] plan,
        Dictionary<string, Technology> technologiesByCode,
        ReferenceLookups lookups,
        int[] touchedTechnologyIds,
        CancellationToken cancellationToken)
    {
        List<TechnologyExperienceType> stored = touchedTechnologyIds.Length == 0
            ? []
            : await context.TechnologyExperienceTypes
                .Where(mapping => touchedTechnologyIds.Contains(mapping.TechnologyId))
                .ToListAsync(cancellationToken);

        foreach (TechnologyPlan item in plan)
        {
            Technology technology = technologiesByCode[item.Dto.Code];

            HashSet<int> desired = [.. item.Dto.ExperienceTypeCodes.Select(code => lookups.ExperienceTypes[code])];

            List<TechnologyExperienceType> current = [.. stored.Where(mapping => mapping.TechnologyId == technology.Id && technology.Id != 0)];

            context.TechnologyExperienceTypes.RemoveRange(current.Where(mapping => !desired.Contains(mapping.ExperienceTypeId)));

            // The mapping carries no payload beyond its own key, so there is nothing to update - only to add or remove.
            foreach (int experienceTypeId in desired.Where(id => !current.Any(mapping => mapping.ExperienceTypeId == id)))
            {
                technology.ExperienceTypes.Add(new TechnologyExperienceType
                {
                    ExperienceTypeId = experienceTypeId
                });
            }
        }
    }

    /// <summary>
    /// Replaces each technology's prerequisite edges, comparing in code-space.
    /// </summary>
    /// <remarks>
    /// Code-space rather than id-space because a prerequisite may point at a Technology this same request is creating, which has no id until the save.
    /// </remarks>
    private async Task SyncPrerequisitesAsync(
        TechnologyPlan[] plan,
        Dictionary<string, Technology> technologiesByCode,
        Dictionary<string, Technology> referencedTechnologies,
        int[] touchedTechnologyIds,
        CancellationToken cancellationToken)
    {
        List<TechnologyPrerequisite> stored = touchedTechnologyIds.Length == 0
            ? []
            : await context.TechnologyPrerequisites
                .Include(prerequisite => prerequisite.PrerequisiteTechnology)
                .Where(prerequisite => touchedTechnologyIds.Contains(prerequisite.TechnologyId))
                .ToListAsync(cancellationToken);

        foreach (TechnologyPlan item in plan)
        {
            Technology technology = technologiesByCode[item.Dto.Code];

            Dictionary<string, TechnologyPrerequisiteKind> desired = item.Dto.Prerequisites
                .ToDictionary(prerequisite => prerequisite.PrerequisiteTechnologyCode, prerequisite => prerequisite.PrerequisiteKind);

            List<TechnologyPrerequisite> current =
                [.. stored.Where(prerequisite => prerequisite.TechnologyId == technology.Id && technology.Id != 0)];

            context.TechnologyPrerequisites.RemoveRange(
                current.Where(prerequisite => !desired.ContainsKey(prerequisite.PrerequisiteTechnology.Code)));

            foreach ((string prerequisiteCode, TechnologyPrerequisiteKind kind) in desired)
            {
                TechnologyPrerequisite? existing =
                    current.FirstOrDefault(prerequisite => prerequisite.PrerequisiteTechnology.Code == prerequisiteCode);

                if (existing is null)
                {
                    // The target may be in this request or already in the database; either way it is linked by navigation so EF resolves both keys.
                    Technology target = technologiesByCode.TryGetValue(prerequisiteCode, out Technology? fromRequest)
                        ? fromRequest
                        : referencedTechnologies[prerequisiteCode];

                    technology.Prerequisites.Add(new TechnologyPrerequisite
                    {
                        PrerequisiteTechnology = target,
                        PrerequisiteKind = kind
                    });
                }
                else
                {
                    existing.PrerequisiteKind = kind;
                }
            }
        }
    }

    /// <summary>
    /// One requested technology with its sort order resolved.
    /// </summary>
    private sealed record TechnologyPlan(UpsertTechnologyDto Dto, int SortOrder);

    /// <summary>
    /// Code-to-id maps for everything a cost, grant or experience mapping can point at.
    /// </summary>
    private sealed record ReferenceLookups(
        Dictionary<string, int> Assets,
        Dictionary<string, int> AssetMetaCategories,
        Dictionary<string, int> UnitComponents,
        Dictionary<string, int> UnitTemplates,
        Dictionary<string, int> MapResourceTypes,
        Dictionary<string, int> ExperienceTypes)
    {
        /// <summary>
        /// Resolves a grant target's code against the catalogue its type names, or null when it does not resolve.
        /// </summary>
        public int? ResolveGrantTarget(TechnologyGrantTargetType targetType, string targetCode)
        {
            Dictionary<string, int> lookup = targetType switch
            {
                TechnologyGrantTargetType.Asset => Assets,
                TechnologyGrantTargetType.AssetMetaCategory => AssetMetaCategories,
                TechnologyGrantTargetType.UnitComponent => UnitComponents,
                TechnologyGrantTargetType.UnitTemplate => UnitTemplates,
                TechnologyGrantTargetType.MapResourceType => MapResourceTypes,
                _ => []
            };

            return lookup.TryGetValue(targetCode, out int id) ? id : null;
        }
    }
}
