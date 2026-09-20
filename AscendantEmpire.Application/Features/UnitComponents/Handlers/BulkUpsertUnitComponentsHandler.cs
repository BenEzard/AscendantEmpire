using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Bulk-upserts unit components (with their full type-specific graph) into the global catalogue.
/// </summary>
/// <remarks>
/// <para>This is the catalogue-transfer path — importing a whole authored catalogue, potentially from another
/// database. Single-component edits from the Entity Setup Tool go through the create/update endpoints instead;
/// both share <see cref="UnitComponentReconciler"/> so the graph rules are identical.</para>
/// <para>Components whose <c>Code</c> is new are inserted; components whose <c>Code</c> already
/// exists are updated in place — scalar fields, the type-specific extension and every child
/// collection (slots, rules, domain modifiers and tag joins) are reconciled from the payload.</para>
/// <para>An existing component's <c>ComponentType</c> is immutable: re-submitting a code with a
/// different type is rejected rather than silently swapping its extension.</para>
/// </remarks>
public record BulkUpsertUnitComponentsRequest(BulkUpsertUnitComponentsDto Catalogue) : IRequest<ServiceResult<BulkUpsertUnitComponentsResult>>;

internal class BulkUpsertUnitComponentsHandler(
    ILogger<BulkUpsertUnitComponentsHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator,
    ICampaignTurnService campaignTurnService
) : IRequestHandler<BulkUpsertUnitComponentsRequest, ServiceResult<BulkUpsertUnitComponentsResult>>
{
    public async Task<ServiceResult<BulkUpsertUnitComponentsResult>> Handle(BulkUpsertUnitComponentsRequest request, CancellationToken cancelToken)
    {
        try
        {
            BulkUpsertUnitComponentsDto catalogue = request.Catalogue;

            // Request-level validation. Collect every problem so the caller gets a complete picture rather than one error at a time.
            List<string> errors = ValidateRequest(catalogue);

            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", errors));
                return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(errors);
            }

            // Bulk-load existing state to avoid N+1 queries. For upsert we need the full graph of any
            // component that already exists (so its children can be reconciled) and any tags that already
            // exist (so they can be re-used rather than duplicated).
            string[] componentCodes = [.. catalogue.Components.Select(c => c.Code)];

            Dictionary<string, UnitComponent> existingByCode = await UnitComponentReconciler
                .WithFullGraph(context.UnitComponents.Where(c => componentCodes.Contains(c.Code)))
                .ToDictionaryAsync(c => c.Code, cancelToken);

            // All tag codes referenced anywhere in the payload, plus those defined in the Tags list.
            string[] referencedTagCodes = [.. catalogue.Components
                .SelectMany(c => c.TagCodes)
                .Concat(catalogue.Tags.Select(t => t.Code))
                .Distinct()];

            Dictionary<string, Tag> tagsByCode = await UnitComponentReconciler.LoadTagsByCodeAsync(context, referencedTagCodes, cancelToken);

            // Validate tag references against payload + DB now that we know what exists.
            List<string> tagErrors = ValidateTagReferences(catalogue, tagsByCode);

            if (tagErrors.Count > 0)
            {
                logger.LogWarning("Tag-reference validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", tagErrors));
                return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(tagErrors);
            }

            // Create any tags defined in the payload that don't already exist; for those that do, let the payload re-classify them (TagType and the organisation fields are authoritative on re-import).
            // Shared with the tags-only upsert the database seed uses, so both create and re-classify identically.
            TagCatalogueUpserter.Upsert(context, catalogue.Tags, tagsByCode);

            // A component's type is immutable, and a slot id must belong to the component carrying it. Both
            // are checked before the graph is touched so a rejected request writes nothing.
            List<string> graphErrors = ValidateAgainstExisting(catalogue, existingByCode);

            if (graphErrors.Count > 0)
            {
                logger.LogWarning("Graph validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", graphErrors));
                return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(graphErrors);
            }

            // Split the payload into inserts (new code) and updates (existing code). New components are
            // built fresh; existing ones are mutated in place with their children reconciled. A
            // code -> entity map (existing + newly built) lets the supersession step wire links by
            // navigation, so EF assigns the foreign keys for brand-new components on save.
            List<string> addedCodes = [];
            List<string> updatedCodes = [];
            Dictionary<string, UnitComponent> componentsByCode = new(existingByCode);

            // Collected before the save: removing a slot deletes the fitted rows that are the only link
            // between an updated component and the templates using it.
            List<int> affectedTemplateIds = await AffectedTemplateRecompiler.FindAffectedTemplateIdsAsync(
                context, [.. existingByCode.Values.Select(component => component.Id)], cancelToken);

            // Slots dropped by the payload are collected across every component and deleted in one batch
            // afterwards, so their dependent rows cost a single query rather than one per body.
            List<UnitComponentSlot> removedSlots = [];

            foreach (UnitComponentDto componentDto in catalogue.Components)
            {
                if (existingByCode.TryGetValue(componentDto.Code, out UnitComponent? existing))
                {
                    UnitComponentReconciler.Update(context, existing, componentDto, tagsByCode, removedSlots);
                    updatedCodes.Add(componentDto.Code);
                }
                else
                {
                    UnitComponent built = UnitComponentReconciler.Build(componentDto, tagsByCode);
                    context.UnitComponents.Add(built);
                    componentsByCode[componentDto.Code] = built;
                    addedCodes.Add(componentDto.Code);
                }
            }

            List<string> unfittedTemplateNames = await UnitComponentReconciler.RemoveSlotsAndDependentsAsync(context, removedSlots, cancelToken);

            if (unfittedTemplateNames.Count > 0)
            {
                logger.LogWarning(
                    "Removing {SlotCount} unit-component slot(s) also cleared fitted components from {TemplateCount} saved template(s): {TemplateNames}.",
                    removedSlots.Count, unfittedTemplateNames.Count, string.Join(", ", unfittedTemplateNames));
            }

            // Reconcile the component supersession (upgrade) links from the payload. Validation runs
            // before any links are staged, so a rejected request still writes nothing.
            if (catalogue.ComponentSupersessions.Count > 0)
            {
                await LoadComponentsReferencedBySupersessionsAsync(catalogue, componentsByCode, cancelToken);

                List<string> supersessionErrors = ValidateComponentSupersessions(catalogue, componentsByCode);

                if (supersessionErrors.Count > 0)
                {
                    logger.LogWarning("Component-supersession validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", supersessionErrors));
                    return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(supersessionErrors);
                }

                await ReconcileComponentSupersessionsAsync(catalogue, componentsByCode, cancelToken);
            }

            // Single SaveChanges => the whole graph (components, extensions, slots, rules, modifiers, tags, joins and supersession links) is written in one transaction.
            await context.SaveChangesAsync(cancelToken);

            // Templates already built on the updated components now hold stale denormalised stats, so their
            // snapshots are refreshed against the committed catalogue. Templates arriving in the same import
            // compile themselves as they are saved, so only pre-existing ones are picked up here.
            List<string> recompileFailures = await AffectedTemplateRecompiler.RecompileAsync(
                context, mediator, campaignTurnService, affectedTemplateIds, cancelToken);

            if (recompileFailures.Count > 0)
            {
                logger.LogWarning(
                    "Components upserted, but {FailureCount} dependent template snapshot(s) could not be recompiled: {Failures}",
                    recompileFailures.Count, string.Join(" | ", recompileFailures));
            }

            logger.LogInformation("Bulk upserted unit components: {AddedCount} added, {UpdatedCount} updated.",
                addedCodes.Count, updatedCodes.Count);

            var result = new BulkUpsertUnitComponentsResult
            {
                AddedCount = addedCodes.Count,
                UpdatedCount = updatedCodes.Count,
                UpdatedCodes = updatedCodes,
                RemovedSlotCount = removedSlots.Count,
                UnfittedTemplateNames = unfittedTemplateNames,
            };

            // 201 when the request was purely new rows; 200 when it touched (updated) existing ones.
            return updatedCodes.Count > 0
                ? ServiceResult<BulkUpsertUnitComponentsResult>.Succeed(result)
                : ServiceResult<BulkUpsertUnitComponentsResult>.Created(result);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(BulkUpsertUnitComponentsHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<BulkUpsertUnitComponentsResult>.InternalServerError(ex, error);
        }
    }

    /// <summary>
    /// Validates the shape of the request: non-empty components, unique codes, type/extension
    /// consistency and field constraints. Returns an empty list when the request is valid.
    /// </summary>
    private static List<string> ValidateRequest(BulkUpsertUnitComponentsDto catalogue)
    {
        List<string> errors = [];

        if (catalogue.Components.Count == 0)
        {
            errors.Add("At least one component must be provided.");
            return errors;
        }

        // Duplicate component codes within the payload.
        string[] duplicateComponentCodes = [.. catalogue.Components
            .GroupBy(c => c.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (duplicateComponentCodes.Length > 0)
        {
            errors.Add($"Duplicate component code(s) in the payload: {string.Join(", ", duplicateComponentCodes)}");
        }

        // Duplicate tag codes within the payload's Tags list.
        string[] duplicateTagCodes = TagCatalogueUpserter.FindDuplicateCodes(catalogue.Tags);

        if (duplicateTagCodes.Length > 0)
        {
            errors.Add($"Duplicate tag code(s) in the payload: {string.Join(", ", duplicateTagCodes)}");
        }

        foreach (UnitComponentDto component in catalogue.Components)
        {
            UnitComponentReconciler.ValidateComponent(component, errors);
        }

        // Each tag's own fields, including the organisation rules shared with the single-tag create/update path, so an imported catalogue cannot slip past the checks the Tag manager enforces.
        foreach (TagDto tag in catalogue.Tags)
        {
            TagCatalogueUpserter.Validate(tag, errors);
        }

        return errors;
    }

    /// <summary>
    /// Validates that every tag code referenced by a component resolves to a tag defined in the
    /// payload or already present in the database.
    /// </summary>
    private static List<string> ValidateTagReferences(BulkUpsertUnitComponentsDto catalogue, IReadOnlyDictionary<string, Tag> existingTagsByCode)
    {
        List<string> errors = [];

        HashSet<string> knownTagCodes = [.. existingTagsByCode.Keys];

        foreach (TagDto tag in catalogue.Tags)
        {
            knownTagCodes.Add(tag.Code);
        }

        foreach (UnitComponentDto component in catalogue.Components)
        {
            UnitComponentReconciler.ValidateTagReferences(component, knownTagCodes, errors);
        }

        return errors;
    }

    /// <summary>
    /// Validates each payload component against the row it will update: its type must be unchanged, and any
    /// slot id it carries must belong to it. Components that don't exist yet are checked for stray slot ids.
    /// </summary>
    private static List<string> ValidateAgainstExisting(BulkUpsertUnitComponentsDto catalogue, IReadOnlyDictionary<string, UnitComponent> existingByCode)
    {
        List<string> errors = [];

        foreach (UnitComponentDto component in catalogue.Components)
        {
            existingByCode.TryGetValue(component.Code, out UnitComponent? existing);

            if (existing is not null)
            {
                UnitComponentReconciler.ValidateTypeUnchanged(component, existing, errors);
            }

            UnitComponentReconciler.ValidateSlots(component, existing, errors);
        }

        return errors;
    }

    /// <summary>
    /// Loads any component referenced by a supersession link that isn't already tracked, because links
    /// may point at components that already exist in the database but aren't part of this payload.
    /// </summary>
    private async Task LoadComponentsReferencedBySupersessionsAsync(
        BulkUpsertUnitComponentsDto catalogue,
        Dictionary<string, UnitComponent> componentsByCode,
        CancellationToken cancelToken)
    {
        string[] missingCodes = [.. catalogue.ComponentSupersessions
            .SelectMany(s => new[] { s.PredecessorCode, s.SuccessorCode })
            .Where(code => !string.IsNullOrWhiteSpace(code) && !componentsByCode.ContainsKey(code))
            .Distinct()];

        if (missingCodes.Length == 0)
        {
            return;
        }

        List<UnitComponent> loaded = await context.UnitComponents
            .Where(c => missingCodes.Contains(c.Code))
            .ToListAsync(cancelToken);

        foreach (UnitComponent component in loaded)
        {
            componentsByCode[component.Code] = component;
        }
    }

    /// <summary>
    /// Validates the component supersession links: codes resolve, no self-supersession, no duplicate
    /// pairs and at most one recommended successor per predecessor. Returns an empty list when valid.
    /// </summary>
    private static List<string> ValidateComponentSupersessions(
        BulkUpsertUnitComponentsDto catalogue,
        IReadOnlyDictionary<string, UnitComponent> componentsByCode)
    {
        List<string> errors = [];

        // Duplicate (predecessor, successor) pairs in the payload.
        string[] duplicatePairs = [.. catalogue.ComponentSupersessions
            .GroupBy(s => (s.PredecessorCode, s.SuccessorCode))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.PredecessorCode} -> {g.Key.SuccessorCode}")];

        if (duplicatePairs.Length > 0)
        {
            errors.Add($"Duplicate component supersession pair(s): {string.Join(", ", duplicatePairs)}");
        }

        foreach (ComponentSupersessionDto link in catalogue.ComponentSupersessions)
        {
            if (string.IsNullOrWhiteSpace(link.PredecessorCode) || string.IsNullOrWhiteSpace(link.SuccessorCode))
            {
                errors.Add("A component supersession is missing its PredecessorCode or SuccessorCode.");
                continue;
            }

            if (link.PredecessorCode == link.SuccessorCode)
            {
                errors.Add($"Component '{link.PredecessorCode}' cannot supersede itself.");
            }

            if (!componentsByCode.ContainsKey(link.PredecessorCode))
            {
                errors.Add($"Component supersession references unknown predecessor code '{link.PredecessorCode}'.");
            }

            if (!componentsByCode.ContainsKey(link.SuccessorCode))
            {
                errors.Add($"Component supersession references unknown successor code '{link.SuccessorCode}'.");
            }
        }

        // A predecessor may flag at most one of its successors as the recommended upgrade.
        string[] predecessorsWithMultipleRecommended = [.. catalogue.ComponentSupersessions
            .Where(s => s.IsRecommended)
            .GroupBy(s => s.PredecessorCode)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (predecessorsWithMultipleRecommended.Length > 0)
        {
            errors.Add($"More than one recommended successor for predecessor(s): {string.Join(", ", predecessorsWithMultipleRecommended)}");
        }

        return errors;
    }

    /// <summary>
    /// Reconciles each mentioned predecessor's outgoing supersession links to exactly those in the
    /// payload. Predecessors absent from the payload are left untouched.
    /// </summary>
    private async Task ReconcileComponentSupersessionsAsync(
        BulkUpsertUnitComponentsDto catalogue,
        IReadOnlyDictionary<string, UnitComponent> componentsByCode,
        CancellationToken cancelToken)
    {
        foreach (IGrouping<string, ComponentSupersessionDto> desiredForPredecessor in catalogue.ComponentSupersessions.GroupBy(s => s.PredecessorCode))
        {
            await UnitComponentReconciler.ReconcileSupersessionsForPredecessorAsync(
                context,
                componentsByCode[desiredForPredecessor.Key],
                [.. desiredForPredecessor],
                componentsByCode,
                cancelToken);
        }
    }
}
