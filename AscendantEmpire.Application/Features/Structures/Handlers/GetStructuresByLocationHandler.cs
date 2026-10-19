using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Structures.Handlers;

public record GetStructuresByLocationRequest(
    int GameId,
    IEnumerable<int> CampaignMapTileIds,
    bool IncludeBattleMapStructures = false
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerStructure>>>;

internal class GetStructuresByLocationRequestValidator : Validator<GetStructuresByLocationRequest>
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

        List<PlayerStructure> structures = await context.StructureInstances
            .Where(si => si.GameId == request.GameId
                      && request.CampaignMapTileIds.Contains(si.CampaignMapTileId)
                      && (request.IncludeBattleMapStructures == false || si.BattleMapTileId != null) // campaign only
                      && si.RemovedOnTurnId == null) // active only
            .Select(si => new PlayerStructure
            {
                StructureInstanceId = si.Id,
                RecordType = RecordType.Structure,
                CampaignMapTileId = si.CampaignMapTileId,
                BattleMapTileId = si.BattleMapTileId,
                XCoord = si.CampaignMap.XCoord,
                YCoord = si.CampaignMap.YCoord,
                StructureId = si.StructureId,
                SlotType = si.SlotType,
                Position = si.Position,
                Name = si.Structure.Name,
                Code = si.Structure.Code,
                // A subquery rather than a navigation because the relationship is configured from the Settlement's side only, and EF expands this to the LEFT JOIN a navigation would have given.
                SettlementName = context.Settlements
                    .Where(settlement => settlement.StructureInstanceId == si.Id)
                    .Select(settlement => settlement.Name)
                    .FirstOrDefault(),
                // Derived on the way out, as in GetPlayerStructuresHandler: the Structure carries work done, and the percentage is the UI's way of reading it.
                CompletionPercent = si.Structure.BuildPointsRequired <= 0
                    ? 0d
                    : 100d * si.BuildPointsProgress / si.Structure.BuildPointsRequired,
                OwnerPlayerId = si.OwnedByPlayerId,
                SightRange = si.Structure.SightRange,
                DetectionDirection = si.Structure.SightDirection,
                MapImagePath = si.Structure.MapImagePath
            })
            .ToListAsync(cancelToken);

        logger.LogInformation("Retrieved {Count} structures for GameId {GameId} and CampaignMapTileIds {TileIds}. IncludeBattleMapStructures: {IncludeBattleMapStructures}",
            structures.Count, request.GameId, string.Join(", ", request.CampaignMapTileIds), request.IncludeBattleMapStructures);

        return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Succeed(structures);
    }
}
