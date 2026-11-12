using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Updates a single existing unit component, with its full type-specific graph, from the Entity Setup Tool.
/// </summary>
/// <remarks>
/// <para>The component is located by its <c>Code</c>, which is immutable here: this endpoint edits a component,
/// it doesn't re-key one. Its <c>ComponentType</c> is immutable too — changing it would mean swapping the
/// type-specific extension underneath everything already referencing the component.</para>
/// <para>Child collections are reconciled, not replaced (see <see cref="UnitComponentReconciler"/>), so slots
/// that survive the edit keep their ids and the templates fitted into them keep working. A slot the payload
/// drops is deleted along with its fittings, which the response reports.</para>
/// </remarks>
public record UpdateUnitComponentRequest(string Code, UpsertUnitComponentDto Request) : IRequest<ServiceResult<UnitComponentView>>;

internal class UpdateUnitComponentHandler(
    ILogger<UpdateUnitComponentHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator,
    ICampaignTurnService campaignTurnService
) : IRequestHandler<UpdateUnitComponentRequest, ServiceResult<UnitComponentView>>
{
    public async Task<ServiceResult<UnitComponentView>> Handle(UpdateUnitComponentRequest request, CancellationToken cancelToken)
    {
        UnitComponentDto dto = request.Request.Component;

        // Shape validation first, so a malformed request never touches the database.
        List<string> errors = [];
        UnitComponentReconciler.ValidateComponent(dto, errors);

        // The route decides which component is being edited; a payload disagreeing with it is a mistake
        // worth surfacing rather than quietly honouring one side.
        if (!string.Equals(request.Code, dto.Code, StringComparison.Ordinal))
        {
            errors.Add($"The component code in the route ('{request.Code}') does not match the code in the payload ('{dto.Code}').");
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(UpdateUnitComponentRequest), string.Join("; ", errors));
            return ServiceResult<UnitComponentView>.Fail(errors);
        }

        UnitComponent? existing = await UnitComponentReconciler
            .WithFullGraph(context.UnitComponents.Where(component => component.Code == request.Code))
            .FirstOrDefaultAsync(cancelToken);

        if (existing is null)
        {
            return ServiceResult<UnitComponentView>.NotFound($"No component exists with code '{request.Code}'.");
        }

        Dictionary<string, Tag> tagsByCode = await UnitComponentReconciler.LoadTagsByCodeAsync(context, dto.TagCodes, cancelToken);

        string[] supplySetCodes = [.. dto.SupplyRequirements.Select(requirement => requirement.SupplySetCode)];
        Dictionary<string, SupplySet> supplySetsByCode = await UnitComponentReconciler.LoadSupplySetsByCodeAsync(context, supplySetCodes, cancelToken);

        string[] ammunitionAssetCodes = [.. (dto.Weapon?.Ammunition ?? []).Select(option => option.AssetCode)];
        Dictionary<string, Asset> assetsByCode = await UnitComponentReconciler.LoadAssetsByCodeAsync(context, ammunitionAssetCodes, cancelToken);

        UnitComponentReconciler.ValidateTagReferences(dto, tagsByCode.Keys, errors);
        UnitComponentReconciler.ValidateSupplyRequirements(dto, supplySetsByCode.Keys, errors);
        UnitComponentReconciler.ValidateWeaponAmmunition(dto, assetsByCode.Keys, errors);
        UnitComponentReconciler.ValidateTypeUnchanged(dto, existing, errors);
        UnitComponentReconciler.ValidateSlots(dto, existing, errors);

        List<ComponentSupersessionDto> supersessions = CreateUnitComponentHandler.ToSupersessionDtos(dto.Code, request.Request.SupersededBy);
        Dictionary<string, UnitComponent> componentsByCode = await LoadSuccessorsAsync(supersessions, cancelToken);

        UnitComponentReconciler.ValidateSupersessionsForPredecessor(dto.Code, supersessions, componentsByCode, errors);

        if (errors.Count > 0)
        {
            logger.LogWarning("Reference validation failed for {Request}. Errors: {Errors}", nameof(UpdateUnitComponentRequest), string.Join("; ", errors));
            return ServiceResult<UnitComponentView>.Fail(errors);
        }

        // Collected before the save: removing a slot deletes the fitted rows that are the only link
        // between this component and the templates using it.
        List<int> affectedTemplateIds = await AffectedTemplateRecompiler.FindAffectedTemplateIdsAsync(context, [existing.Id], cancelToken);

        List<UnitComponentSlot> removedSlots = [];
        UnitComponentReconciler.Update(context, existing, dto, tagsByCode, supplySetsByCode, assetsByCode, removedSlots);

        List<string> unfittedTemplateNames = await UnitComponentReconciler.RemoveSlotsAndDependentsAsync(context, removedSlots, cancelToken);

        if (unfittedTemplateNames.Count > 0)
        {
            logger.LogWarning(
                "Removing {SlotCount} slot(s) from component '{Code}' also cleared fitted components from {TemplateCount} saved template(s): {TemplateNames}.",
                removedSlots.Count, existing.Code, unfittedTemplateNames.Count, string.Join(", ", unfittedTemplateNames));
        }

        await UnitComponentReconciler.ReconcileSupersessionsForPredecessorAsync(context, existing, supersessions, componentsByCode, cancelToken);

        // Single SaveChanges => the component, its extensions, its slots, the cascaded deletions and the
        // supersession links all land in one transaction.
        await context.SaveChangesAsync(cancelToken);

        // Snapshots of templates built on this component are now stale. Recompiling after the commit
        // means each one reads the component as it actually is; a template whose assembly no longer
        // validates is reported rather than undoing a save the caller has already been told nothing about.
        List<string> recompileFailures = await AffectedTemplateRecompiler.RecompileAsync(
            context, mediator, campaignTurnService, affectedTemplateIds, cancelToken);

        if (recompileFailures.Count > 0)
        {
            logger.LogWarning(
                "Component '{Code}' saved, but {FailureCount} dependent template snapshot(s) could not be recompiled: {Failures}",
                existing.Code, recompileFailures.Count, string.Join(" | ", recompileFailures));
        }

        logger.LogInformation("Updated unit component '{Code}' (id {Id}).", existing.Code, existing.Id);

        return ServiceResult<UnitComponentView>.Succeed(new UnitComponentView
        {
            Id = existing.Id,
            Code = existing.Code,
            DisplayName = existing.DisplayName,
            ComponentType = existing.ComponentType,
            SlotIds = UnitComponentReconciler.OrderedSlotIds(existing),
            RemovedSlotCount = removedSlots.Count,
            UnfittedTemplateNames = unfittedTemplateNames,
        });
    }

    /// <summary>
    /// Loads the components named as supersession successors, keyed by code. Codes that don't resolve are
    /// simply absent, which validation reports as unknown successors.
    /// </summary>
    private async Task<Dictionary<string, UnitComponent>> LoadSuccessorsAsync(
        IReadOnlyCollection<ComponentSupersessionDto> supersessions,
        CancellationToken cancelToken)
    {
        string[] successorCodes = [.. supersessions
            .Select(link => link.SuccessorCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct()];

        if (successorCodes.Length == 0)
        {
            return [];
        }

        return await context.UnitComponents
            .Where(component => successorCodes.Contains(component.Code))
            .ToDictionaryAsync(component => component.Code, cancelToken);
    }
}
