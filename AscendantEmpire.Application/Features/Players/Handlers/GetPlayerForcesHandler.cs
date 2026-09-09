using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Units.GetPlayerForces;
using AscendantEmpire.Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Players.Handlers;

public record GetPlayerForcesRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerForce>>>;

internal class GetPlayerForcesRequestValidator : Validator<GetPlayerForcesRequest>
{
    public GetPlayerForcesRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

internal class GetPlayerForcesHandler(
    ILogger<GetPlayerForcesHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayerForcesRequest, ServiceResult<IReadOnlyCollection<PlayerForce>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerForce>>> Handle(GetPlayerForcesRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetPlayerForcesRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetPlayerForcesHandler)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<PlayerForce>>.Fail(errors.Prepend(error));
            }

            ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult = await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancelToken);

            if (gameContextResult.SuccessValue is not { Count: > 0 } gameContexts)
            {
                string error = $"No in-progress game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
                logger.LogWarning("No in-progress game found for Game Id: {gameId} and Player Id: {playerId}.", request.GameId, request.PlayerId);
                return ServiceResult<IReadOnlyCollection<PlayerForce>>.NotFound(error);
            }

            int gameId = gameContexts.First().GameId;
            int playerId = gameContexts.First().Players.First().PlayerId;

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<PlayerForce> playerUnits = await context.UnitInstances
                .Where(pu => pu.GameId == gameId && pu.PlayerId == playerId)
                .Select(pu => new PlayerForce
                {
                    UnitInstanceId = pu.Id,
                    RecordType = RecordType.Unit,
                    PlayerId = pu.PlayerId,
                    CampaignMapTileId = pu.CampaignMapTileId,
                    BattleMapTileId = pu.BattleMapTileId,
                    CampaignXCoord = pu.CampaignMap.XCoord,
                    CampaignYCoord = pu.CampaignMap.YCoord,
                    SightRange = (int)(pu.UnitTemplate.Compiled!.TotalSight / ScaledAmount.Scale),
                    MovementDomain = pu.UnitTemplate.Compiled!.MovementDomain,
                    MapImagePath = pu.UnitTemplate.Compiled!.MapImagePath,
                    HealthyTroops = pu.HealthyCount,
                    InjuredTroops = pu.InjuredCount,
                    InitialStrength = pu.InitialStrength,
                    UnitTemplateId = pu.UnitTemplateId,
                    // Set for the same reason as the by-location projection: every PlayerForce should describe the Force fully, so a future caller of this
                    // handler doesn't silently inherit a default false. Spawning seeds CampaignMovementRemaining, and the start of the owning Player's
                    // Turn replenishes it.
                    HasCampaignMovementRemaining = pu.CampaignMovementRemaining > 0,
                    // The raw fixed-point value converted to whole movement points. Divided inline rather than through ScaledAmount because this runs
                    // as SQL; the scale constant is shared so the two representations cannot drift apart.
                    CampaignMovementRemaining = pu.CampaignMovementRemaining / (decimal)ScaledAmount.Scale,
                    CampaignMovementPoints = pu.UnitTemplate.Compiled!.CampaignMovementPoints / (decimal)ScaledAmount.Scale,
                    BattleMovementRemaining = pu.BattleMovementRemaining / (decimal)ScaledAmount.Scale,
                })
                .ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<PlayerForce>>.Succeed(playerUnits);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing GetPlayerUnitsRequest.");
            return ServiceResult<IReadOnlyCollection<PlayerForce>>.Fail("An unexpected error occurred while retrieving player units.");
        }
    }
}
