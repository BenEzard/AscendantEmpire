using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Dev design tool: updates an existing global/common unit template in the shared catalogue.
/// </summary>
/// <remarks>
/// <para>The template is located by its <c>Id</c>; a missing id, or an id belonging to a player-owned custom
/// template, is a not-found. The <c>Code</c> may be changed, but only to one not already used by another
/// global template (the code is the unique natural key). No unlock check is applied, as with create.</para>
/// <para>The update and a recompile of the template's <c>CompiledUnitTemplate</c> snapshot (via <see cref="CompileUnitTemplateHandler"/>)
/// are persisted atomically. Global templates aren't game-scoped, so the snapshot is stamped with turn <c>0</c>.</para>
/// </remarks>
public record UpdateGlobalUnitTemplateRequest(int Id, SaveGlobalUnitTemplateDto Template) : IRequest<ServiceResult<GlobalUnitTemplateView>>;

internal class UpdateGlobalUnitTemplateHandler(
    ILogger<UpdateGlobalUnitTemplateHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
) : IRequestHandler<UpdateGlobalUnitTemplateRequest, ServiceResult<GlobalUnitTemplateView>>
{
    public async Task<ServiceResult<GlobalUnitTemplateView>> Handle(UpdateGlobalUnitTemplateRequest request, CancellationToken cancelToken)
    {
        try
        {
            SaveGlobalUnitTemplateDto dto = request.Template;

            List<string> codeErrors = ValidateCode(dto.Code);

            if (codeErrors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(UpdateGlobalUnitTemplateRequest), string.Join("; ", codeErrors));
                return ServiceResult<GlobalUnitTemplateView>.Fail(codeErrors);
            }

            UnitTemplate? existing = await context.UnitTemplates
                .Include(t => t.Components)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancelToken);

            // A player-owned custom template (PlayerId set) is not a global template — treat it as not found here.
            if (existing is null || existing.PlayerId != null)
            {
                return ServiceResult<GlobalUnitTemplateView>.NotFound($"No global template exists with id {request.Id}.");
            }

            if (existing.Code != dto.Code)
            {
                bool codeTaken = await context.UnitTemplates.AnyAsync(t => t.Code == dto.Code && t.Id != request.Id, cancelToken);

                if (codeTaken)
                {
                    return ServiceResult<GlobalUnitTemplateView>.Conflict($"A template with code '{dto.Code}' already exists.");
                }
            }

            ServiceResult<UnitTemplateAssembly.ResolvedAssembly> resolved = await UnitTemplateAssembly.ResolveAsync(
                context, playerId: null, dto.DisplayName, dto.MapImagePath, dto.BaseCode, dto.SlotComponentCodes, cancelToken);

            if (!resolved.HasSucceeded)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}",
                    nameof(UpdateGlobalUnitTemplateRequest), string.Join("; ", resolved.Errors));
                return new ServiceResult<GlobalUnitTemplateView> { StatusCode = resolved.StatusCode, Errors = resolved.Errors };
            }

            (UnitComponent baseComponent, List<UnitTemplateComponent> fitted) = resolved.SuccessValue!;

            existing.Code = dto.Code;
            existing.DisplayName = dto.DisplayName;
            existing.Description = dto.Description;
            existing.MapImagePath = dto.MapImagePath;
            existing.BodyComponentId = baseComponent.Id;

            // Update + recompile atomically: the snapshot is the template's read model, so it must never lag the
            // template it summarizes. CompileUnitTemplateHandler shares this request scope's context, so one
            // transaction covers both saves; disposing without Commit (any failure path) rolls the update back too.
            await using var transaction = await context.Database.BeginTransactionAsync(cancelToken);

            // Replace the fitted-component collection wholesale; the child FK is NoAction, so the old
            // rows must be deleted explicitly rather than just unlinked.
            context.UnitTemplateComponents.RemoveRange(existing.Components);
            existing.Components = fitted;

            await context.SaveChangesAsync(cancelToken);

            ServiceResult<int> compiled = await mediator.Send(
                new CompileUnitTemplateRequest(UnitTemplateId: existing.Id, CompiledOnTurn: 0), cancelToken);

            if (!compiled.HasSucceeded)
            {
                logger.LogWarning("Compilation failed for {Request}. Errors: {Errors}",
                    nameof(UpdateGlobalUnitTemplateRequest), string.Join("; ", compiled.Errors));
                return new ServiceResult<GlobalUnitTemplateView> { StatusCode = compiled.StatusCode, Errors = compiled.Errors };
            }

            await transaction.CommitAsync(cancelToken);

            logger.LogInformation("Updated global unit template '{Code}' (id {Id}).", existing.Code, existing.Id);

            return ServiceResult<GlobalUnitTemplateView>.Succeed(ToView(existing, baseComponent));
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(UpdateGlobalUnitTemplateHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<GlobalUnitTemplateView>.InternalServerError(ex, error);
        }
    }

    private static List<string> ValidateCode(string code)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(code))
        {
            errors.Add("The template is missing its Code.");
        }
        else if (code.Length > UnitTemplate.CODE_MAX_LENGTH)
        {
            errors.Add($"Template code exceeds {UnitTemplate.CODE_MAX_LENGTH} characters.");
        }

        return errors;
    }

    /// <summary>
    /// Projects a persisted <see cref="UnitTemplate"/> (plus its already-loaded body component) onto the
    /// API <see cref="GlobalUnitTemplateView"/> contract.
    /// </summary>
    private static GlobalUnitTemplateView ToView(UnitTemplate template, UnitComponent bodyComponent) => new()
    {
        Id = template.Id,
        Code = template.Code!,
        DisplayName = template.DisplayName,
        Description = template.Description,
        MapImagePath = template.MapImagePath,
        BodyComponentId = template.BodyComponentId,
        BodyComponentCode = bodyComponent.Code,
    };
}
