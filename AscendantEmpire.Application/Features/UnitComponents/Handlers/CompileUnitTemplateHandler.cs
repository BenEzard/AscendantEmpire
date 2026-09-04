using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Compiles a saved <see cref="UnitTemplate"/> into its denormalized <see cref="CompiledUnitTemplate"/> snapshot:
/// aggregates the body + fitted components into typed stats, captures the resolved component list and capability tags, and upserts the snapshot row.
/// </summary>
/// <remarks>
/// Invoked from every template-writing flow (player/global create + update, catalogue import), inside the caller's transaction and
/// sharing its scoped context, so a template is never persisted without a current snapshot. Returns the compiled template id on success.
/// </remarks>
public record CompileUnitTemplateRequest(int UnitTemplateId, int CompiledOnTurn) : IRequest<ServiceResult<int>>;

internal class CompileUnitTemplateHandler(
    ILogger<CompileUnitTemplateHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<CompileUnitTemplateRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(CompileUnitTemplateRequest request, CancellationToken cancelToken)
    {
        try
        {
            if (request.UnitTemplateId <= 0)
            {
                return ServiceResult<int>.Fail("A valid template id is required.");
            }

            // Load the whole template graph: body (+ its body extension and tags) and every fitted component (+ its weapon/armour/tags) so we can aggregate without further round-trips.
            UnitTemplate? template = await context.UnitTemplates
                .Include(t => t.BodyComponent).ThenInclude(b => b.Body)
                // A body may carry its own inherent weapon/armour, which contribute to the unit's stats.
                .Include(t => t.BodyComponent).ThenInclude(b => b.Weapon)
                .Include(t => t.BodyComponent).ThenInclude(b => b.Armour)
                .Include(t => t.BodyComponent).ThenInclude(b => b.Tags).ThenInclude(ct => ct.Tag)
                .Include(t => t.Components).ThenInclude(tc => tc.UnitComponent).ThenInclude(c => c.Weapon)
                .Include(t => t.Components).ThenInclude(tc => tc.UnitComponent).ThenInclude(c => c.Armour)
                .Include(t => t.Components).ThenInclude(tc => tc.UnitComponent).ThenInclude(c => c.Tags).ThenInclude(ct => ct.Tag)
                // The slot (with its rules) is needed to validate the assembled tree before compiling.
                .Include(t => t.Components).ThenInclude(tc => tc.UnitComponentSlot).ThenInclude(s => s.Rules)
                .FirstOrDefaultAsync(t => t.Id == request.UnitTemplateId, cancelToken);

            if (template is null)
            {
                return ServiceResult<int>.NotFound($"Template {request.UnitTemplateId} was not found.");
            }

            UnitComponent bodyComponent = template.BodyComponent;

            if (bodyComponent.Body is not { } body)
            {
                return ServiceResult<int>.Fail($"Template {request.UnitTemplateId}'s body component is not a body.");
            }

            // Validate the fitted-component tree (correct owner per slot, rules satisfied, no double-fills) before compiling, so a malformed assembly never produces a snapshot.
            List<string> assemblyErrors = SlotAssemblyValidator.Validate(template.BodyComponentId, template.Components);

            if (assemblyErrors.Count > 0)
            {
                logger.LogWarning("Assembly validation failed for template {TemplateId}. Errors: {Errors}",
                    request.UnitTemplateId, 
                    string.Join("; ", assemblyErrors));

                return ServiceResult<int>.Fail(assemblyErrors);
            }

            // Aggregate stats across the body and every fitted component.
            var attack = new DamageProfile();
            var resistance = new DamageProfile();

            long totalWeight = bodyComponent.Weight;
            long totalSight = bodyComponent.Sight ?? 0;
            long totalDetection = bodyComponent.Detection ?? 0;
            long totalVisibility = bodyComponent.Visibility ?? 0;

            // Carried weight is the fitted components only, so it deliberately starts at zero rather than at the body's own weight. It feeds the
            // battle-map movement derivation, where the body's weight must not count against it — see BattleMovementCalculator.
            long carriedWeight = 0;

            // The body's own inherent weapon/armour (if any) contribute just like a fitted weapon/armour.
            if (bodyComponent.Weapon is { } bodyWeapon)
            {
                AddProfile(attack, bodyWeapon.Attack);
            }

            if (bodyComponent.Armour is { } bodyArmour)
            {
                AddProfile(resistance, bodyArmour.Resistance);
            }

            foreach (UnitTemplateComponent fitted in template.Components)
            {
                UnitComponent component = fitted.UnitComponent;

                totalWeight += component.Weight;
                carriedWeight += component.Weight;
                totalSight += component.Sight ?? 0;
                totalDetection += component.Detection ?? 0;
                totalVisibility += component.Visibility ?? 0;

                if (component.Weapon is { } weapon)
                {
                    AddProfile(attack, weapon.Attack);
                }

                if (component.Armour is { } armour)
                {
                    AddProfile(resistance, armour.Resistance);
                }
            }

            // Resolved component snapshot: the body (no slot, no parent) plus each fitted component.
            var bodySnapshot = new CompiledUnitTemplateComponent
            {
                UnitComponentSlotId = null,
                UnitComponentId = bodyComponent.Id,
                Code = bodyComponent.Code,
                DisplayName = bodyComponent.DisplayName,
                ComponentType = bodyComponent.ComponentType,
            };

            // One snapshot row per fitted component (each fitted directly into a body slot).
            List<CompiledUnitTemplateComponent> fittedSnapshots = [.. template.Components.Select(fitted =>
                new CompiledUnitTemplateComponent
                {
                    UnitComponentSlotId = fitted.UnitComponentSlotId,
                    UnitComponentId = fitted.UnitComponentId,
                    Code = fitted.UnitComponent.Code,
                    DisplayName = fitted.UnitComponent.DisplayName,
                    ComponentType = fitted.UnitComponent.ComponentType,
                })];

            List<CompiledUnitTemplateComponent> snapshotComponents = [bodySnapshot, .. fittedSnapshots];

            // Capabilities: the distinct tags carried across the body and fitted components.
            IEnumerable<Tag> allTags = bodyComponent.Tags.Select(t => t.Tag)
                .Concat(template.Components.SelectMany(fitted => fitted.UnitComponent.Tags.Select(t => t.Tag)));

            List<CompiledUnitTemplateCapability> snapshotCapabilities = [.. allTags
                .GroupBy(tag => tag.Code)
                .Select(group => new CompiledUnitTemplateCapability
                {
                    TagCode = group.Key,
                    DisplayName = group.First().DisplayName,
                })];

            // Resolve the template's military organisation tag from its components (there is no field on the
            // template itself): a non-body component's organisation tag (e.g. camping-goods -> Settler)
            // overrides the body's default (e.g. human -> Infantry). More than one organisation tag at the
            // same precedence level is ambiguous, so the compile fails rather than guessing.
            ServiceResult<int?> organisationTagResult = ResolveOrganisationTag(bodyComponent, template.Components);

            if (organisationTagResult.HasSucceeded == false)
            {
                logger.LogWarning("Organisation-tag resolution failed for template {TemplateId}. Errors: {Errors}",
                    request.UnitTemplateId,
                    string.Join("; ", organisationTagResult.Errors));

                return ServiceResult<int>.Fail(organisationTagResult.Errors);
            }

            // Battle-map movement is derived here rather than authored on the body: power minus what the unit is carrying, floored at zero.
            long battlefieldMovementPoints = BattleMovementCalculator.CalculateBattlefieldMovementPoints(body.Power, carriedWeight);

            await UpsertSnapshotAsync(template, body, totalWeight: totalWeight, totalSight: totalSight, totalDetection: totalDetection,
                totalVisibility: totalVisibility, battlefieldMovementPoints: battlefieldMovementPoints, attack, resistance, snapshotComponents,
                snapshotCapabilities, organisationTagResult.SuccessValue, request.CompiledOnTurn, cancelToken);

            return ServiceResult<int>.Succeed(template.Id);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(CompileUnitTemplateHandler) + ".";
            logger.LogError(ex, error);

            return ServiceResult<int>.InternalServerError(ex, error);
        }
    }

    /// <summary>
    /// Resolves the template's military organisation tag from its components. A non-body component's
    /// organisation tag beats the body's default; two organisation tags at the same precedence level fail.
    /// Succeeds with null when no component carries an organisation tag at all.
    /// </summary>
    private static ServiceResult<int?> ResolveOrganisationTag(UnitComponent bodyComponent, ICollection<UnitTemplateComponent> fittedComponents)
    {
        // Distinct by tag id — the same tag carried by two fitted components (or twice on the body) is
        // agreement, not ambiguity.
        List<Tag> bodyOrgTags = [.. bodyComponent.Tags
            .Select(componentTag => componentTag.Tag)
            .Where(tag => tag.TagType == TagType.MilitaryOrganisationalGroup)
            .DistinctBy(tag => tag.Id)];

        List<Tag> fittedOrgTags = [.. fittedComponents
            .SelectMany(fitted => fitted.UnitComponent.Tags.Select(componentTag => componentTag.Tag))
            .Where(tag => tag.TagType == TagType.MilitaryOrganisationalGroup)
            .DistinctBy(tag => tag.Id)];

        if (fittedOrgTags.Count > 1)
        {
            return ServiceResult<int?>.Fail(
                $"The template's fitted components carry more than one military organisation tag ({string.Join(", ", fittedOrgTags.Select(t => $"'{t.Code}'"))}) — the organisation is ambiguous.");
        }

        if (bodyOrgTags.Count > 1)
        {
            return ServiceResult<int?>.Fail(
                $"The template's body carries more than one military organisation tag ({string.Join(", ", bodyOrgTags.Select(t => $"'{t.Code}'"))}) — the organisation is ambiguous.");
        }

        // Precedence: a fitted component's tag overrides the body's default.
        int? organisationTagId = fittedOrgTags.FirstOrDefault()?.Id ?? bodyOrgTags.FirstOrDefault()?.Id;

        return ServiceResult<int?>.Succeed(organisationTagId);
    }

    /// <summary>
    /// Inserts a fresh snapshot, or updates the existing one in place and fully replaces its children (the child FKs are NoAction, so old rows are deleted explicitly).
    /// </summary>
    private async Task UpsertSnapshotAsync(
        UnitTemplate template,
        UnitComponentBody body,
        long totalWeight,
        long totalSight,
        long totalDetection,
        long totalVisibility,
        long battlefieldMovementPoints,
        DamageProfile attack,
        DamageProfile resistance,
        List<CompiledUnitTemplateComponent> components,
        List<CompiledUnitTemplateCapability> capabilities,
        int? organisationTagId,
        int compiledOnTurn,
        CancellationToken cancelToken)
    {
        CompiledUnitTemplate? existing = await context.CompiledUnitTemplates
            .Include(c => c.Components)
            .Include(c => c.Capabilities)
            .FirstOrDefaultAsync(c => c.UnitTemplateId == template.Id, cancelToken);

        if (existing is null)
        {
            context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
            {
                UnitTemplateId = template.Id,
                DisplayName = template.DisplayName,
                MapImagePath = template.MapImagePath,
                BodyComponentId = template.BodyComponentId,
                MovementDomain = body.MovementDomain,
                OrganisationTagId = organisationTagId,
                TotalWeight = totalWeight,
                TotalSight = totalSight,
                TotalDetection = totalDetection,
                TotalVisibility = totalVisibility,
                // Campaign movement/power/hitpoints are raw fixed-point longs (Scale 1,000) copied straight from the body. Battle-map movement is
                // the exception: it is derived from power and carried weight by the caller, so the body's authored value is ignored.
                CampaignMovementPoints = body.CampaignMovementPoints,
                BattlefieldMovementPoints = battlefieldMovementPoints,
                Power = body.Power,
                Hitpoints = body.Hitpoints,
                Attack = attack,
                Resistance = resistance,
                CompiledOnTurn = compiledOnTurn,
                Components = components,
                Capabilities = capabilities,
            });
        }
        else
        {
            existing.DisplayName = template.DisplayName;
            existing.MapImagePath = template.MapImagePath;
            existing.BodyComponentId = template.BodyComponentId;
            existing.MovementDomain = body.MovementDomain;
            existing.OrganisationTagId = organisationTagId;
            existing.TotalWeight = totalWeight;
            existing.TotalSight = totalSight;
            existing.TotalDetection = totalDetection;
            existing.TotalVisibility = totalVisibility;
            // Campaign movement/power/hitpoints are raw fixed-point longs (Scale 1,000) copied straight from the body. Battle-map movement is
            // the exception: it is derived from power and carried weight by the caller, so the body's authored value is ignored.
            existing.CampaignMovementPoints = body.CampaignMovementPoints;
            existing.BattlefieldMovementPoints = battlefieldMovementPoints;
            existing.Power = body.Power;
            existing.Hitpoints = body.Hitpoints;
            existing.Attack = attack;
            existing.Resistance = resistance;
            existing.CompiledOnTurn = compiledOnTurn;

            // Replace children wholesale; NoAction FKs => delete the old rows explicitly.
            context.CompiledUnitTemplateComponents.RemoveRange(existing.Components);
            context.CompiledUnitTemplateCapabilities.RemoveRange(existing.Capabilities);
            existing.Components = components;
            existing.Capabilities = capabilities;
        }

        await context.SaveChangesAsync(cancelToken);
    }

    private static void AddProfile(DamageProfile target, DamageProfile source)
    {
        target.Slash += source.Slash;
        target.Pierce += source.Pierce;
        target.Crush += source.Crush;
        target.Burn += source.Burn;
        target.Poison += source.Poison;
    }
}
