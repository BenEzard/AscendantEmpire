using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Units.GetPlayerTroops;
using AscendantEmpire.Domain.Enums;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Players;

public record GetPlayerTroopsRequest(
    Guid GameGuid,
    Guid PlayerGuid
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerTroop>>>;

internal class GetPlayerTroopsRequestValidator : AbstractValidator<GetPlayerTroopsRequest>
{
    public GetPlayerTroopsRequestValidator()
    {
        RuleFor(x => x.GameGuid)
            .NotEmpty().WithMessage("GameGuid is required.");
        RuleFor(r => r.GameGuid)
         .Must(guid => guid != Guid.Empty)
         .WithMessage("GameGuid must be a valid non-empty GUID.");

        RuleFor(x => x.PlayerGuid)
            .NotEmpty().WithMessage("PlayerGuid is required.");
        RuleFor(r => r.PlayerGuid)
         .Must(guid => guid != Guid.Empty)
         .WithMessage("PlayerGuid must be a valid non-empty GUID.");
    }
}

internal class GetPlayerTroopsHandler(
    ILogger<GetPlayerTroopsHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayerTroopsRequest, ServiceResult<IReadOnlyCollection<PlayerTroop>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerTroop>>> Handle(GetPlayerTroopsRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetPlayerTroopsRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetPlayerTroopsHandler)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<PlayerTroop>>.Fail(errors.Prepend(error));
            }

            ServiceResult<IReadOnlyCollection<OpenGame>> openGameResult = await mediator.Send(new GetOpenGamesRequest(request.GameGuid, request.PlayerGuid), cancelToken);

            if (openGameResult.SuccessValue is not { Count: > 0 } openGames)
            {
                string error = $"No open game found for GameGuid: {request.GameGuid} and PlayerGuid: {request.PlayerGuid}.";
                logger.LogWarning("No open game found for GameGuid: {gameGuid} and PlayerGuid: {playerGuid}.", request.GameGuid, request.PlayerGuid);
                return ServiceResult<IReadOnlyCollection<PlayerTroop>>.NotFound(error);
            }

            int gameId = openGames.First().GameId;
            int playerId = openGames.First().Players.First().PlayerId;

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<PlayerTroop> playerUnits = await context.TroopInstances
                .Include(ti => ti.Stats)
                .Where(pu => pu.GameId == gameId && pu.PlayerId == playerId)
                .Select(pu => new PlayerTroop
                {
                    TroopInstanceId = pu.Id,
                    RecordType = RecordType.Unit,
                    PlayerId = pu.PlayerId,
                    CampaignMapTileId = pu.CampaignMapTileId,
                    BattleMapTileId = pu.BattleMapTileId,
                    CampaignXCoord = pu.CampaignMap.XCoord,
                    CampaignYCoord = pu.CampaignMap.YCoord,
                    SightRange = pu.Stats.SightRange,
                    MapImagePath = pu.Stats.MapImagePath,
                    // Health 
                    UnitDesignId = pu.UnitDesignId,

                })
                .ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<PlayerTroop>>.Succeed(playerUnits);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing GetPlayerUnitsRequest.");
            return ServiceResult<IReadOnlyCollection<PlayerTroop>>.Fail("An unexpected error occurred while retrieving player units.");
        }
    }
}
