using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
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
/// The template is located by its <c>Id</c>. Only its own owner (matching game and player) may update it;
/// any other caller — or an unknown id — gets a not-found rather than a permissions error, so a custom
/// template's existence isn't leaked to callers who don't own it.
/// </remarks>
public record UpdateUnitTemplateRequest(int GameId, int PlayerId, int TemplateId, SaveUnitTemplateDto Template) : IRequest<ServiceResult<UnitTemplateView>>;

internal class UpdateUnitTemplateHandler(
    ILogger<UpdateUnitTemplateHandler> logger,
    AscendantEmpireContext context
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

            // Replace the fitted-component collection wholesale; the child FK is NoAction, so the old
            // rows must be deleted explicitly rather than just unlinked (mirrors CompileUnitTemplateHandler's
            // handling of CompiledUnitTemplateComponents).
            context.UnitTemplateComponents.RemoveRange(existing.Components);
            existing.Components = fitted;

            await context.SaveChangesAsync(cancelToken);

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
