using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Units.GetPlayerTroops;
using AscendantEmpire.Domain.Enums;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Players.Handlers;

public record GetPlayerTroopsRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerTroop>>>;

internal class GetPlayerTroopsRequestValidator : AbstractValidator<GetPlayerTroopsRequest>
{
    public GetPlayerTroopsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
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

            ServiceResult<IReadOnlyCollection<OpenGame>> openGameResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId), cancelToken);

            if (openGameResult.SuccessValue is not { Count: > 0 } openGames)
            {
                string error = $"No open game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
                logger.LogWarning("No open game found for Game Id: {gameId} and Player Id: {playerId}.", request.GameId, request.PlayerId);
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
                    HealthyTroops = pu.HealthyCount,
                    InjuredTroops = pu.InjuredCount,
                    InitialStrength = pu.InitialStrength,
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
