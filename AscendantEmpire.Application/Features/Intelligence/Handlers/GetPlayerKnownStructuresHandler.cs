using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownStructures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Intelligence.Handlers;

public record GetPlayerKnownStructuresRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<KnownStructure>>>;

internal class GetPlayerKnownStructuresRequestValidator : Validator<GetPlayerKnownStructuresRequest>
{
    public GetPlayerKnownStructuresRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// Every Structure a Player has watched change, as they last saw it, stripped to the fidelity each sighting earned.
/// </summary>
/// <remarks>
/// <para>
/// The Structure counterpart of <see cref="GetPlayerKnownForcesHandler"/>, and returned whole in the same way, including Structures the Player can see right now: which markers to hide under live
/// vision is the client's decision.
/// </para>
/// <para>
/// Every row goes through <see cref="SightingDisclosure"/>, the same projection the report feed uses, so the map layer cannot leak what the feed withholds.
/// </para>
/// </remarks>
internal class GetPlayerKnownStructuresHandler(
    ILogger<GetPlayerKnownStructuresHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader
) : IRequestHandler<GetPlayerKnownStructuresRequest, ServiceResult<IReadOnlyCollection<KnownStructure>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<KnownStructure>>> Handle(GetPlayerKnownStructuresRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetPlayerKnownStructuresRequestValidator();
            ValidationResult validationResult = validator.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetPlayerKnownStructuresRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<IReadOnlyCollection<KnownStructure>>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The token already ties the caller to this Player, but a Player that is not in the Game is a request for nothing, and says so rather than answering with an empty map layer.
            bool isPlayerInGame = await context.Players.AnyAsync(player => player.Id == request.PlayerId && player.GameId == request.GameId, cancelToken);

            if (isPlayerInGame == false)
            {
                string error = $"Player Id {request.PlayerId} was not found in Game Id {request.GameId}.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<IReadOnlyCollection<KnownStructure>>.NotFound(error);
            }

            CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, request.GameId, cancelToken);

            if (geometry is null)
            {
                string error = $"Game Id {request.GameId} was not found while reading known Structures.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<IReadOnlyCollection<KnownStructure>>.NotFound(error);
            }

            // Oldest sighting first, so the client can draw fresher markers on top when two share a tile.
            var knownStructureRows = await (from known in context.PlayerKnownStructures.AsNoTracking()
                                            join turn in context.PlayerTurns.AsNoTracking()
                                                on known.LastSeenPlayerTurnId equals turn.Id
                                            where known.GameId == request.GameId && known.PlayerId == request.PlayerId
                                            orderby known.LastSeenPlayerTurnId, known.LastSeenTurnSequence, known.Id
                                            select new { Known = known, turn.RoundNumber }).ToListAsync(cancelToken);

            List<KnownStructure> disclosedStructures =
            [
                .. knownStructureRows.Select(row => SightingDisclosure.Disclose(new StoredKnownStructure
                {
                    Status = row.Known.Status,
                    Fidelity = row.Known.Fidelity,
                    Location = geometry.GetCoord(row.Known.CampaignMapTileId),
                    LastSeenPlayerTurnId = row.Known.LastSeenPlayerTurnId,
                    LastSeenRoundNumber = row.RoundNumber,
                    LastSeenTurnSequence = row.Known.LastSeenTurnSequence,
                    StillVisible = row.Known.StillVisible,
                    Subject = new SightingSubject
                    {
                        StructureInstanceId = row.Known.StructureInstanceId,
                        StructureId = row.Known.StructureId,
                        OwnerPlayerId = row.Known.OwnerPlayerId,
                        CompletionPercent = row.Known.ObservedCompletionPercent
                    }
                }))
            ];

            return ServiceResult<IReadOnlyCollection<KnownStructure>>.Succeed(disclosedStructures);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(GetPlayerKnownStructuresRequest));

            return ServiceResult<IReadOnlyCollection<KnownStructure>>.Fail("An unexpected error occurred while retrieving known Structures.");
        }
    }
}
