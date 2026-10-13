using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Creates a single unit component, with its full type-specific graph, from the Entity Setup Tool.
/// </summary>
/// <remarks>
/// <para>The component's <c>Code</c> is the unique natural key, so a request whose code already exists is
/// rejected as a conflict rather than silently updated — use the update endpoint for that. This mirrors the
/// tag create/update pair, and is why the dev tool no longer has to route single edits through the catalogue
/// bulk importer.</para>
/// <para>Tags are referenced by code only, and supersession successors must already exist: defining new tags
/// or forward-declaring components inline remains a catalogue-import concern.</para>
/// </remarks>
public record CreateUnitComponentRequest(UpsertUnitComponentDto Request) : IRequest<ServiceResult<UnitComponentView>>;

internal class CreateUnitComponentHandler(
    ILogger<CreateUnitComponentHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<CreateUnitComponentRequest, ServiceResult<UnitComponentView>>
{
    public async Task<ServiceResult<UnitComponentView>> Handle(CreateUnitComponentRequest request, CancellationToken cancelToken)
    {
        UnitComponentDto dto = request.Request.Component;

        // Shape validation first, so a malformed request never touches the database.
        List<string> errors = [];
        UnitComponentReconciler.ValidateComponent(dto, errors);

        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(CreateUnitComponentRequest), string.Join("; ", errors));
            return ServiceResult<UnitComponentView>.Fail(errors);
        }

        // Code is the unique natural key — reject a duplicate before inserting.
        bool codeExists = await context.UnitComponents.AnyAsync(component => component.Code == dto.Code, cancelToken);

        if (codeExists)
        {
            return ServiceResult<UnitComponentView>.Conflict($"A component with code '{dto.Code}' already exists.");
        }

        Dictionary<string, Tag> tagsByCode = await UnitComponentReconciler.LoadTagsByCodeAsync(context, dto.TagCodes, cancelToken);
        UnitComponentReconciler.ValidateTagReferences(dto, tagsByCode.Keys, errors);

        // A brand-new component owns no slots, so any slot id the payload carries is stale or misdirected.
        UnitComponentReconciler.ValidateSlots(dto, existing: null, errors);

        // Successors must already be in the catalogue; this component isn't saved yet, so it is added to
        // the lookup by navigation and EF resolves its foreign key on save.
        List<ComponentSupersessionDto> supersessions = ToSupersessionDtos(dto.Code, request.Request.SupersededBy);
        Dictionary<string, UnitComponent> componentsByCode = await LoadSuccessorsAsync(supersessions, cancelToken);

        UnitComponentReconciler.ValidateSupersessionsForPredecessor(dto.Code, supersessions, componentsByCode, errors);

        if (errors.Count > 0)
        {
            logger.LogWarning("Reference validation failed for {Request}. Errors: {Errors}", nameof(CreateUnitComponentRequest), string.Join("; ", errors));
            return ServiceResult<UnitComponentView>.Fail(errors);
        }

        UnitComponent component = UnitComponentReconciler.Build(dto, tagsByCode);
        context.UnitComponents.Add(component);
        componentsByCode[dto.Code] = component;

        await UnitComponentReconciler.ReconcileSupersessionsForPredecessorAsync(context, component, supersessions, componentsByCode, cancelToken);

        // Single SaveChanges => the component, its extensions and its supersession links land together.
        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Created unit component '{Code}' (id {Id}).", component.Code, component.Id);

        return ServiceResult<UnitComponentView>.Created(new UnitComponentView
        {
            Id = component.Id,
            Code = component.Code,
            DisplayName = component.DisplayName,
            ComponentType = component.ComponentType,
            SlotIds = UnitComponentReconciler.OrderedSlotIds(component),
        });
    }

    /// <summary>
    /// Expands the component-relative upgrade links onto the flat predecessor/successor DTO the reconciler
    /// shares with the catalogue importer.
    /// </summary>
    internal static List<ComponentSupersessionDto> ToSupersessionDtos(string predecessorCode, IReadOnlyCollection<ComponentSupersessionLinkDto> links)
    {
        return [.. links.Select(link => new ComponentSupersessionDto
        {
            PredecessorCode = predecessorCode,
            SuccessorCode = link.SuccessorCode,
            IsRecommended = link.IsRecommended,
        })];
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
