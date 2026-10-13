using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownForces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Intelligence.Handlers;

public record GetPlayerKnownForcesRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<KnownForce>>>;

internal class GetPlayerKnownForcesRequestValidator : Validator<GetPlayerKnownForcesRequest>
{
    public GetPlayerKnownForcesRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// Every Force a Player has watched, as they last saw it, stripped to the fidelity each sighting earned.
/// </summary>
/// <remarks>
/// <para>
/// Returned whole, including Forces the Player can see right now. Which markers to hide under live vision is the client's decision, since only it knows what it is currently drawing.
/// </para>
/// <para>
/// Every row goes through <see cref="SightingDisclosure"/>, the same projection the report feed uses, so the map layer cannot leak what the feed withholds.
/// </para>
/// </remarks>
internal class GetPlayerKnownForcesHandler(
    ILogger<GetPlayerKnownForcesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader
) : IRequestHandler<GetPlayerKnownForcesRequest, ServiceResult<IReadOnlyCollection<KnownForce>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<KnownForce>>> Handle(GetPlayerKnownForcesRequest request, CancellationToken cancelToken)
    {
        // Validate the request object
        var validator = new GetPlayerKnownForcesRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (validationResult.IsValid == false)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetPlayerKnownForcesRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<IReadOnlyCollection<KnownForce>>.Fail(errors.Prepend(error));
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        bool isPlayerInGame = await context.Players.AnyAsync(player => player.Id == request.PlayerId && player.GameId == request.GameId, cancelToken);

        if (isPlayerInGame == false)
        {
            string error = $"Player Id {request.PlayerId} was not found in Game Id {request.GameId}.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<IReadOnlyCollection<KnownForce>>.NotFound(error);
        }

        CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, request.GameId, cancelToken);

        if (geometry is null)
        {
            string error = $"Game Id {request.GameId} was not found while reading known Forces.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<IReadOnlyCollection<KnownForce>>.NotFound(error);
        }

        // Oldest sighting first, so the client can draw fresher markers on top when two share a tile.
        var knownForceRows = await (from known in context.PlayerKnownForces.AsNoTracking()
                                    join turn in context.PlayerTurns.AsNoTracking()
                                        on known.LastSeenPlayerTurnId equals turn.Id
                                    where known.GameId == request.GameId && known.PlayerId == request.PlayerId
                                    orderby known.LastSeenPlayerTurnId, known.LastSeenTurnSequence, known.Id
                                    select new { Known = known, turn.RoundNumber }).ToListAsync(cancelToken);

        List<KnownForce> disclosedForces =
        [
            .. knownForceRows.Select(row => SightingDisclosure.Disclose(new StoredKnownForce
            {
                Status = row.Known.Status,
                Fidelity = row.Known.Fidelity,
                LastSeenLocation = geometry.GetCoord(row.Known.LastSeenCampaignMapTileId),
                LastSeenPlayerTurnId = row.Known.LastSeenPlayerTurnId,
                LastSeenRoundNumber = row.RoundNumber,
                LastSeenTurnSequence = row.Known.LastSeenTurnSequence,
                ExitDirection = row.Known.ExitDirection,
                StillVisible = row.Known.StillVisible,
                Subject = new SightingSubject
                {
                    UnitInstanceId = row.Known.UnitInstanceId,
                    OwnerPlayerId = row.Known.OwnerPlayerId,
                    UnitTemplateId = row.Known.UnitTemplateId,
                    HealthyTroops = row.Known.HealthyTroops
                }
            }))
        ];

        return ServiceResult<IReadOnlyCollection<KnownForce>>.Succeed(disclosedForces);
    }
}
