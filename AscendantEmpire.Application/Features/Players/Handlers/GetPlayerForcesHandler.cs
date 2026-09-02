using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Units.GetPlayerForces;
using AscendantEmpire.Domain.Enums;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Players.Handlers;

public record GetPlayerForcesRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerForce>>>;

internal class GetPlayerForcesRequestValidator : AbstractValidator<GetPlayerForcesRequest>
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

            ServiceResult<IReadOnlyCollection<OpenGame>> openGameResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId), cancelToken);

            if (openGameResult.SuccessValue is not { Count: > 0 } openGames)
            {
                string error = $"No open game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
                logger.LogWarning("No open game found for Game Id: {gameId} and Player Id: {playerId}.", request.GameId, request.PlayerId);
                return ServiceResult<IReadOnlyCollection<PlayerForce>>.NotFound(error);
            }

            int gameId = openGames.First().GameId;
            int playerId = openGames.First().Players.First().PlayerId;

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
                    // Sight range in tiles, derived from the compiled snapshot's aggregated detection
                    // (fixed-point, Scale 1,000 — see the template compilation). Stand-in until a dedicated
                    // sight-range stat is compiled. The snapshot always exists: a Unit cannot be spawned
                    // from an uncompiled template.
                    SightRange = (int)(pu.UnitTemplate.Compiled!.TotalDetection / 1000),
                    MapImagePath = pu.UnitTemplate.Compiled!.MapImagePath,
                    HealthyTroops = pu.HealthyCount,
                    InjuredTroops = pu.InjuredCount,
                    InitialStrength = pu.InitialStrength,
                    UnitTemplateId = pu.UnitTemplateId,
                    // Set for the same reason as the by-location projection: every PlayerForce should describe the Force fully, so a future caller of this
                    // handler doesn't silently inherit a default false. Spawning seeds MovementPointsRemaining (F-9); turn rollover does not yet
                    // replenish it (TM-3).
                    HasMovementRemaining = pu.MovementPointsRemaining > 0,
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
