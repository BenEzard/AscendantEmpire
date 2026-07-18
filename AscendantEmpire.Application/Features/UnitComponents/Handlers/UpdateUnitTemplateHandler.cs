using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Updates an existing custom (player-owned) unit template: re-assembles its base and fitted components
/// from the request and replaces the previous assignment wholesale.
/// </summary>
/// <remarks>
/// <para>The template is located by its <c>Id</c>. Only its own owner (matching game and player) may update it;
/// any other caller — or an unknown id — gets a not-found rather than a permissions error, so a custom
/// template's existence isn't leaked to callers who don't own it.</para>
/// <para>The update and a recompile of the template's <c>CompiledUnitTemplate</c> snapshot (via <see cref="CompileUnitTemplateHandler"/>)
/// are persisted atomically, stamped with the game's current campaign turn.</para>
/// </remarks>
public record UpdateUnitTemplateRequest(int GameId, int PlayerId, int TemplateId, SaveUnitTemplateDto Template) : IRequest<ServiceResult<UnitTemplateView>>;

internal class UpdateUnitTemplateHandler(
    ILogger<UpdateUnitTemplateHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator,
    ICampaignTurnService campaignTurnService
) : IRequestHandler<UpdateUnitTemplateRequest, ServiceResult<UnitTemplateView>>
{
    public async Task<ServiceResult<UnitTemplateView>> Handle(UpdateUnitTemplateRequest request, CancellationToken cancelToken)
    {
        try
        {
            UnitTemplate? existing = await context.UnitTemplates
                .Include(t => t.Components)
                .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancelToken);

            if (existing is null || existing.GameId != request.GameId || existing.PlayerId != request.PlayerId)
            {
                return ServiceResult<UnitTemplateView>.NotFound($"No custom template exists with id {request.TemplateId}.");
            }

            SaveUnitTemplateDto dto = request.Template;

            ServiceResult<UnitTemplateAssembly.ResolvedAssembly> resolved = await UnitTemplateAssembly.ResolveAsync(
                context, request.PlayerId, dto.DisplayName, dto.MapImagePath, dto.BaseCode, dto.SlotComponentCodes, cancelToken);

            if (!resolved.HasSucceeded)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}",
                    nameof(UpdateUnitTemplateRequest), string.Join("; ", resolved.Errors));
                return new ServiceResult<UnitTemplateView> { StatusCode = resolved.StatusCode, Errors = resolved.Errors };
            }

            (UnitComponent baseComponent, List<UnitTemplateComponent> fitted) = resolved.SuccessValue!;

            existing.DisplayName = dto.DisplayName;
            existing.Description = dto.Description;
            existing.MapImagePath = dto.MapImagePath;
            existing.BodyComponentId = baseComponent.Id;

            // Update + recompile atomically: the snapshot is the template's read model, so it must never lag the
            // template it summarizes. CompileUnitTemplateHandler shares this request scope's context, so one
            // transaction covers both saves; disposing without Commit (any failure path) rolls the update back too.
            await using var transaction = await context.Database.BeginTransactionAsync(cancelToken);

            // Replace the fitted-component collection wholesale; the child FK is NoAction, so the old
            // rows must be deleted explicitly rather than just unlinked (mirrors CompileUnitTemplateHandler's
            // handling of CompiledUnitTemplateComponents).
            context.UnitTemplateComponents.RemoveRange(existing.Components);
            existing.Components = fitted;

            await context.SaveChangesAsync(cancelToken);

            // Stamp the refreshed snapshot with the game's current campaign turn (0 if the campaign hasn't started yet).
            int currentTurn = await campaignTurnService.GetCurrentRoundNumberAsync(request.GameId, cancelToken);

            ServiceResult<int> compiled = await mediator.Send(
                new CompileUnitTemplateRequest(UnitTemplateId: existing.Id, CompiledOnTurn: currentTurn), cancelToken);

            if (!compiled.HasSucceeded)
            {
                logger.LogWarning("Compilation failed for {Request}. Errors: {Errors}",
                    nameof(UpdateUnitTemplateRequest), string.Join("; ", compiled.Errors));
                return new ServiceResult<UnitTemplateView> { StatusCode = compiled.StatusCode, Errors = compiled.Errors };
            }

            await transaction.CommitAsync(cancelToken);

            logger.LogInformation("Updated custom unit template '{DisplayName}' (id {Id}) for player {PlayerId}.",
                existing.DisplayName, existing.Id, request.PlayerId);

            return ServiceResult<UnitTemplateView>.Succeed(ToView(existing, baseComponent));
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(UpdateUnitTemplateHandler) + ".";
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
