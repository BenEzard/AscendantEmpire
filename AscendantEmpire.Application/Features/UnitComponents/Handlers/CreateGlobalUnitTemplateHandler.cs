using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Dev design tool: creates a new global/common unit template in the shared catalogue.
/// </summary>
/// <remarks>
/// <para>Unlike <see cref="CreateUnitTemplateHandler"/> (player-owned custom templates), a global template has
/// no game/player, its <c>Code</c> is the unique natural key, and no unlock check is applied — the dev
/// tool authors the catalogue itself rather than a specific player's already-unlocked parts.</para>
/// <para>The template and its <c>CompiledUnitTemplate</c> snapshot (via <see cref="CompileUnitTemplateHandler"/>) are persisted
/// atomically. Global templates aren't game-scoped, so the snapshot is stamped with turn <c>0</c>.</para>
/// </remarks>
public record CreateGlobalUnitTemplateRequest(SaveGlobalUnitTemplateDto Template) : IRequest<ServiceResult<GlobalUnitTemplateView>>;

internal class CreateGlobalUnitTemplateHandler(
    ILogger<CreateGlobalUnitTemplateHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
) : IRequestHandler<CreateGlobalUnitTemplateRequest, ServiceResult<GlobalUnitTemplateView>>
{
    public async Task<ServiceResult<GlobalUnitTemplateView>> Handle(CreateGlobalUnitTemplateRequest request, CancellationToken cancelToken)
    {
        try
        {
            SaveGlobalUnitTemplateDto dto = request.Template;

            List<string> codeErrors = ValidateCode(dto.Code);

            if (codeErrors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(CreateGlobalUnitTemplateRequest), string.Join("; ", codeErrors));
                return ServiceResult<GlobalUnitTemplateView>.Fail(codeErrors);
            }

            // Code is the unique natural key — reject a duplicate before resolving/persisting anything else.
            bool codeExists = await context.UnitTemplates.AnyAsync(t => t.Code == dto.Code, cancelToken);

            if (codeExists)
            {
                return ServiceResult<GlobalUnitTemplateView>.Conflict($"A template with code '{dto.Code}' already exists.");
            }

            ServiceResult<UnitTemplateAssembly.ResolvedAssembly> resolved = await UnitTemplateAssembly.ResolveAsync(
                context, playerId: null, dto.DisplayName, dto.MapImagePath, dto.BaseCode, dto.SlotComponentCodes, cancelToken);

            if (!resolved.HasSucceeded)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}",
                    nameof(CreateGlobalUnitTemplateRequest), string.Join("; ", resolved.Errors));
                return new ServiceResult<GlobalUnitTemplateView> { StatusCode = resolved.StatusCode, Errors = resolved.Errors };
            }

            (UnitComponent baseComponent, List<UnitTemplateComponent> fitted) = resolved.SuccessValue!;

            var template = new UnitTemplate
            {
                Code = dto.Code,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                GameId = null,
                PlayerId = null,
                BodyComponentId = baseComponent.Id,
                MapImagePath = dto.MapImagePath,
                Components = fitted,
            };

            // Save + compile atomically: the snapshot is the template's read model, so a template must never exist without one.
            // CompileUnitTemplateHandler shares this request scope's context, so one transaction covers both saves;
            // disposing without Commit (any failure path) rolls the template back too.
            await using var transaction = await context.Database.BeginTransactionAsync(cancelToken);

            context.UnitTemplates.Add(template);
            await context.SaveChangesAsync(cancelToken);

            ServiceResult<int> compiled = await mediator.Send(
                new CompileUnitTemplateRequest(UnitTemplateId: template.Id, CompiledOnTurn: 0), cancelToken);

            if (!compiled.HasSucceeded)
            {
                logger.LogWarning("Compilation failed for {Request}. Errors: {Errors}",
                    nameof(CreateGlobalUnitTemplateRequest), string.Join("; ", compiled.Errors));
                return new ServiceResult<GlobalUnitTemplateView> { StatusCode = compiled.StatusCode, Errors = compiled.Errors };
            }

            await transaction.CommitAsync(cancelToken);

            logger.LogInformation("Created global unit template '{Code}' (id {Id}).", template.Code, template.Id);

            return ServiceResult<GlobalUnitTemplateView>.Created(ToView(template, baseComponent));
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(CreateGlobalUnitTemplateHandler) + ".";
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
