using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Saves a custom (Player-owned) Unit Template from an assembled design: a base body plus the components fitted into its Slots.
/// </summary>
/// <remarks>
/// <para>The Template and its <c>CompiledUnitTemplate</c> snapshot (via <see cref="CompileUnitTemplateHandler"/>) are persisted atomically,
/// stamped with the game's current campaign turn — every saved template always has a compiled read model.</para>
/// <para>Names are the player's natural key for their own designs, so this is an upsert rather than a plain insert. A name already in use answers 409
/// with a <see cref="TemplateNameConflict"/> describing whether confirming would help; re-sending with <c>OverwriteExisting</c> set then hands the save
/// to <see cref="UpdateUnitTemplateHandler"/>, which owns the one update path. The caller never needs to know the existing design's id.</para>
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

            // Name clashes are settled before anything is resolved or written: either they end the request with a conflict the caller can act on, or
            // they redirect it to the update path.
            ServiceResult<UnitTemplateView>? nameOutcome = await ResolveNameClashAsync(request, cancelToken);

            if (nameOutcome != null)
            {
                return nameOutcome;
            }

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
    /// Settles what an already-taken name means for this save: a conflict to answer with, the result of the overwrite it was confirmed for, or null when
    /// the name is free and the create should carry on.
    /// </summary>
    /// <remarks>
    /// <para>Three outcomes, distinguished by who can resolve the clash. A global/common design owning the name is terminal — the shared catalogue is
    /// not a player's to overwrite. One of the player's own with Units already fielded is terminal too: those Units read their stats from the design's
    /// compiled snapshot, so redrawing it would silently re-equip units standing on the map. Anything else is the player's to confirm.</para>
    /// <para><c>OverwriteExisting</c> confirms an intent rather than granting a permission, so it is only consulted once the terminal cases are out of
    /// the way — a caller that sets it on a clash they could never resolve is still refused.</para>
    /// </remarks>
    private async Task<ServiceResult<UnitTemplateView>?> ResolveNameClashAsync(CreateUnitTemplateRequest request, CancellationToken cancelToken)
    {
        SaveUnitTemplateDto dto = request.Template;

        // A blank name is the shape validator's error to report, with the rest of the shape problems, rather than a conflict.
        if (string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return null;
        }

        UnitTemplate? global = await UnitTemplateNameLookup.FindGlobalTemplateByNameAsync(context, dto.DisplayName, cancelToken);

        if (global != null)
        {
            return ServiceResult<UnitTemplateView>.Conflict(
                $"A standard design is already called \"{global.DisplayName}\". Choose a different name.",
                new TemplateNameConflict { CanOverwrite = false, ExistingDisplayName = global.DisplayName });
        }

        UnitTemplate? existing = await UnitTemplateNameLookup.FindPlayerTemplateByNameAsync(
            context,
            request.GameId,
            request.PlayerId,
            dto.DisplayName,
            excludeTemplateId: null,
            cancelToken);

        if (existing == null)
        {
            return null;
        }

        int instanceCount = await UnitTemplateNameLookup.CountUnitInstancesAsync(context, existing.Id, cancelToken);

        if (instanceCount > 0)
        {
            return ServiceResult<UnitTemplateView>.Conflict(
                $"\"{existing.DisplayName}\" cannot be changed while {FormatUnitCount(instanceCount)} built from it. "
                + "Choose a different name, or disband those Units first.",
                new TemplateNameConflict { CanOverwrite = false, ExistingDisplayName = existing.DisplayName, InstanceCount = instanceCount });
        }

        if (!dto.OverwriteExisting)
        {
            return ServiceResult<UnitTemplateView>.Conflict(
                $"You already have a design called \"{existing.DisplayName}\".",
                new TemplateNameConflict { CanOverwrite = true, ExistingDisplayName = existing.DisplayName });
        }

        // Confirmed. The update handler owns the one update path — including re-checking everything above — so the overwrite goes through it rather than
        // being reimplemented here.
        logger.LogInformation("Overwriting custom unit template '{DisplayName}' (id {Id}) for player {PlayerId}.",
            existing.DisplayName,
            existing.Id,
            request.PlayerId);

        return await mediator.Send(new UpdateUnitTemplateRequest(request.GameId, request.PlayerId, existing.Id, dto), cancelToken);
    }

    /// <summary>
    /// Renders a Unit count as the subject of a sentence, so the message reads naturally at one Unit as well as at many.
    /// </summary>
    private static string FormatUnitCount(int instanceCount)
    {
        return instanceCount == 1 ? "1 unit is" : $"{instanceCount} units are";
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
