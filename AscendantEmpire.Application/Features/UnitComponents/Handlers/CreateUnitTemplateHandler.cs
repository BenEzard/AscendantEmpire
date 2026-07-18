using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Creates a new custom (Player-owned) Unit Template from an assembled design: a base body plus the components fitted into its Slots.
/// </summary>
/// <remarks>
/// The Template and its <c>CompiledUnitTemplate</c> snapshot (via <see cref="CompileUnitTemplateHandler"/>) are persisted atomically,
/// stamped with the game's current campaign turn — every saved template always has a compiled read model.
/// </remarks>
public record CreateUnitTemplateRequest(int GameId, int PlayerId, SaveUnitTemplateDto Template) : IRequest<ServiceResult<UnitTemplateView>>;

internal class CreateUnitTemplateHandler(
    ILogger<CreateUnitTemplateHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator,
    ICampaignTurnService campaignTurnService
) : IRequestHandler<CreateUnitTemplateRequest, ServiceResult<UnitTemplateView>>
{
    public async Task<ServiceResult<UnitTemplateView>> Handle(CreateUnitTemplateRequest request, CancellationToken cancelToken)
    {
        try
        {
            SaveUnitTemplateDto dto = request.Template;

            ServiceResult<UnitTemplateAssembly.ResolvedAssembly> resolved = await UnitTemplateAssembly.ResolveAsync(
                context, request.PlayerId, dto.DisplayName, dto.MapImagePath, dto.BaseCode, dto.SlotComponentCodes, cancelToken);

            if (!resolved.HasSucceeded)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}",
                    nameof(CreateUnitTemplateRequest), 
                    string.Join("; ", resolved.Errors));

                return new ServiceResult<UnitTemplateView> { StatusCode = resolved.StatusCode, Errors = resolved.Errors };
            }

            (UnitComponent baseComponent, List<UnitTemplateComponent> fitted) = resolved.SuccessValue!;

            var template = new UnitTemplate
            {
                Code = null,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                GameId = request.GameId,
                PlayerId = request.PlayerId,
                BodyComponentId = baseComponent.Id,
                MapImagePath = dto.MapImagePath,
                Components = fitted,
            };

            // Save + compile atomically: the snapshot is the template's read model, so a template must never exist without one.
            // CompileUnitTemplateHandler shares this request scope's context, so one transaction covers both saves;
            // disposing without Commit (any failure path) rolls the template back too.
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

            context.UnitTemplates.Add(template);
            await context.SaveChangesAsync(cancelToken);

            // Stamp the snapshot with the game's current campaign turn (0 if the campaign hasn't started yet).
            int currentTurn = await campaignTurnService.GetCurrentRoundNumberAsync(request.GameId, cancelToken);

            ServiceResult<int> compiled = await mediator.Send(new CompileUnitTemplateRequest(UnitTemplateId: template.Id, CompiledOnTurn: currentTurn), cancelToken);

            if (!compiled.HasSucceeded)
            {
                logger.LogWarning("Compilation failed for {Request}. Errors: {Errors}", nameof(CreateUnitTemplateRequest), string.Join("; ", compiled.Errors));

                return new ServiceResult<UnitTemplateView> { StatusCode = compiled.StatusCode, Errors = compiled.Errors };
            }

            await transaction.CommitAsync(cancelToken);

            logger.LogInformation("Created custom unit template '{DisplayName}' (id {Id}) for player {PlayerId}.",
                template.DisplayName, 
                template.Id, 
                request.PlayerId);

            return ServiceResult<UnitTemplateView>.Created(ToView(template, baseComponent));
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(CreateUnitTemplateHandler) + ".";
            logger.LogError(ex, error);

            return ServiceResult<UnitTemplateView>.InternalServerError(ex, error);
        }
    }

    /// <summary>
    /// Projects a persisted <see cref="UnitTemplate"/> (plus its already-loaded body component) onto the
    /// API <see cref="UnitTemplateView"/> contract.
    /// </summary>
    private static UnitTemplateView ToView(UnitTemplate template, UnitComponent bodyComponent) => new()
    {
        Id = template.Id,
        DisplayName = template.DisplayName,
        Description = template.Description,
        MapImagePath = template.MapImagePath,
        BodyComponentId = template.BodyComponentId,
        BodyComponentCode = bodyComponent.Code,
    };
}
