using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.Domain.Enums;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Structures.Handlers;

public record GetStructuresByLocationRequest(
    int GameId,
    IEnumerable<int> CampaignMapTileIds,
    bool IncludeBattleMapStructures = false
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerStructure>>>;

internal class GetStructuresByLocationRequestValidator : AbstractValidator<GetStructuresByLocationRequest>
{
    public GetStructuresByLocationRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.CampaignMapTileIds)
            .NotNull().WithMessage("At least one Campaign Map Tile Id is required.")
            .Must(ids => ids != null && ids.Any()).WithMessage("At least one Campaign Map Tile Id is required.");

        RuleForEach(x => x.CampaignMapTileIds)
            .GreaterThan(0).WithMessage("Campaign Map Tile Ids must be greater than 0.");
    }
}

internal class GetStructuresByLocationHandler(
    ILogger<GetStructuresByLocationHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetStructuresByLocationRequest, ServiceResult<IReadOnlyCollection<PlayerStructure>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerStructure>>> Handle(GetStructuresByLocationRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetStructuresByLocationRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetStructuresByLocationRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<PlayerStructure> structures = await (from si in context.StructureInstances.Include(si => si.Structure)
                                                      join tile in context.CampaignMaps
                                                        on si.CampaignMapTileId equals tile.CampaignMapId
                                                      where si.GameId == request.GameId
                                                        && request.CampaignMapTileIds.Contains(si.CampaignMapTileId)
                                                        && (request.IncludeBattleMapStructures == false || si.BattleMapTileId != null) // campaign only
                                                        && si.RemovedOnTurnId == null // active only
                                                      select new PlayerStructure
                                                      {
                                                          StructureInstanceId = si.Id,
                                                          RecordType = RecordType.Structure,
                                                          CampaignMapTileId = si.CampaignMapTileId,
                                                          BattleMapTileId = si.BattleMapTileId,
                                                          XCoord = tile.XCoord,
                                                          YCoord = tile.YCoord,
                                                          StructureId = si.StructureId,
                                                          SlotType = si.SlotType,
                                                          Position = si.Position,
                                                          Name = si.Structure.Name,
                                                          Code = si.Structure.Code,
                                                          CompletionPercent = si.CompletionPercent,
                                                          OwnerId = si.OwnedByPlayerId,
                                                          SightRange = si.Structure.SightRange,
                                                          DetectionDirection = si.Structure.SightDirection,
                                                          MapImagePath = si.Structure.MapImagePath
                                                      }).ToListAsync(cancelToken);

            logger.LogInformation("Retrieved {Count} structures for GameId {GameId} and CampaignMapTileIds {TileIds}. IncludeBattleMapStructures: {IncludeBattleMapStructures}",
                structures.Count, request.GameId, string.Join(", ", request.CampaignMapTileIds), request.IncludeBattleMapStructures);

            return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Succeed(structures);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing {RequestName}.", nameof(GetStructuresByLocationRequest));
            return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Fail("An unexpected error occurred while retrieving structures.");
        }
    }
}
