using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Compiles a finalized <see cref="UnitTemplate"/> into its denormalized <see cref="CompiledUnitTemplate"/> snapshot: 
/// aggregates the body + fitted components into typed stats, captures the resolved component list and capability tags, and upserts the snapshot row.
/// </summary>
/// <remarks>
/// Intended to be invoked from the (future) template-finalization flow — i.e. when a template is prototyped. Returns the compiled template id on success.
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

            // Validate the fitted-component tree (correct owner per slot, rules satisfied, no double-fills)
            // before compiling, so a malformed assembly never produces a snapshot.
            List<string> assemblyErrors = SlotAssemblyValidator.Validate(template.BodyComponentId, template.Components);

            if (assemblyErrors.Count > 0)
            {
                logger.LogWarning("Assembly validation failed for template {TemplateId}. Errors: {Errors}",
                    request.UnitTemplateId, string.Join("; ", assemblyErrors));
                return ServiceResult<int>.Fail(assemblyErrors);
            }

            // Aggregate stats across the body and every fitted component.
            var attack = new DamageProfile();
            var resistance = new DamageProfile();

            long totalWeight = bodyComponent.Weight;
            long totalDetection = bodyComponent.Detection ?? 0;
            long totalVisibility = bodyComponent.Visibility ?? 0;

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

            // One snapshot row per fitted component, keyed by its source id so we can wire parent links.
            Dictionary<int, CompiledUnitTemplateComponent> snapshotByFittedId = template.Components.ToDictionary(
                fitted => fitted.Id,
                fitted => new CompiledUnitTemplateComponent
                {
                    UnitComponentSlotId = fitted.UnitComponentSlotId,
                    UnitComponentId = fitted.UnitComponentId,
                    Code = fitted.UnitComponent.Code,
                    DisplayName = fitted.UnitComponent.DisplayName,
                    ComponentType = fitted.UnitComponent.ComponentType,
                });

            // Wire each fitted snapshot to its parent snapshot via navigation, so EF assigns ParentId on
            // save. Root-fitted components (no parent) keep ParentId null, matching the live model.
            foreach (UnitTemplateComponent fitted in template.Components)
            {
                if (fitted.ParentTemplateComponentId is { } parentId)
                {
                    snapshotByFittedId[fitted.Id].Parent = snapshotByFittedId[parentId];
                }
            }

            List<CompiledUnitTemplateComponent> snapshotComponents = [bodySnapshot, .. snapshotByFittedId.Values];

            // Capabilities: the distinct tags carried across the body and fitted components.
            IEnumerable<Tag> allTags = bodyComponent.Tags.Select(t => t.Tag)
                .Concat(template.Components.SelectMany(fitted => fitted.UnitComponent.Tags.Select(t => t.Tag)));

            List<CompiledUnitTemplateCapability> snapshotCapabilities = allTags
                .GroupBy(tag => tag.Code)
                .Select(group => new CompiledUnitTemplateCapability
                {
                    TagCode = group.Key,
                    DisplayName = group.First().DisplayName,
                })
                .ToList();

            await UpsertSnapshotAsync(template, body, totalWeight, totalDetection, totalVisibility, attack, resistance,
                snapshotComponents, snapshotCapabilities, request.CompiledOnTurn, cancelToken);

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
    /// Inserts a fresh snapshot, or updates the existing one in place and fully replaces its children (the child FKs are NoAction, so old rows are deleted explicitly).
    /// </summary>
    private async Task UpsertSnapshotAsync(
        UnitTemplate template,
        UnitComponentBody body,
        long totalWeight,
        long totalDetection,
        long totalVisibility,
        DamageProfile attack,
        DamageProfile resistance,
        List<CompiledUnitTemplateComponent> components,
        List<CompiledUnitTemplateCapability> capabilities,
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
                TotalWeight = totalWeight,
                TotalDetection = totalDetection,
                TotalVisibility = totalVisibility,
                CampaignMovementPoints = body.CampaignMovementPoints,
                BattlefieldMovementPoints = body.BattlefieldMovementPoints,
                MinStrength = body.MinStrength,
                MaxStrength = body.MaxStrength,
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
            existing.TotalWeight = totalWeight;
            existing.TotalDetection = totalDetection;
            existing.TotalVisibility = totalVisibility;
            existing.CampaignMovementPoints = body.CampaignMovementPoints;
            existing.BattlefieldMovementPoints = body.BattlefieldMovementPoints;
            existing.MinStrength = body.MinStrength;
            existing.MaxStrength = body.MaxStrength;
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
