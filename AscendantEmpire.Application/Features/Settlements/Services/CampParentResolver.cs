using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Settlements.Services;

/// <inheritdoc cref="ICampParentResolver"/>
internal sealed class CampParentResolver(ILogger<CampParentResolver> logger) : ICampParentResolver
{
    /// <inheritdoc/>
    public async Task<ServiceResult<int?>> ResolveAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        int campStructureInstanceId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Read straight off GameIndex rather than through IGameContextService. That service answers "is this Player active in this in-progress Game", which the caller has
        // already established and which would refuse a Game that has not started; all that is wanted here is the shape of the board. See G-174 for giving this read a home
        // of its own, which is where it duplicates ISightDataReader.GetGeometryAsync.
        MapShapeRow? mapShape = await context.GameIndex
            .AsNoTracking()
            .Where(game => game.Id == gameId)
            .Select(game => new MapShapeRow(game.CampaignMapOffsetId, game.MapWidth, game.MapHeight, game.HorizontalWrap, game.VerticalWrap))
            .FirstOrDefaultAsync(cancelToken);

        if (mapShape is null)
        {
            logger.LogError("Game Id {GameId} could not be read, so no parent settlement could be resolved for a camp in it.", gameId);

            return ServiceResult<int?>.InternalServerError("The game's map could not be read, so the camp could not be given a settlement to feed.");
        }

        int? campTileId = await context.StructureInstances
            .AsNoTracking()
            .Where(structure => structure.Id == campStructureInstanceId)
            .Select(structure => (int?)structure.CampaignMapTileId)
            .FirstOrDefaultAsync(cancelToken);

        if (campTileId is null)
        {
            logger.LogError("Structure Instance Id {StructureInstanceId} could not be read, so the camp standing on it has no tile to measure distances from.",
                campStructureInstanceId);

            return ServiceResult<int?>.InternalServerError("The camp's own structure could not be read, so the camp could not be given a settlement to feed.");
        }

        // Standing settlements of a tier above camp that this Player holds now. Ownership is the structure's, so a village taken from them is no longer offered; and a camp
        // is never a parent, which is also why the camp being founded cannot come back as its own answer.
        List<CandidateRow> candidates = await context.Settlements
            .AsNoTracking()
            .Where(candidate => candidate.GameId == gameId
                && candidate.Tier != SettlementTier.Camp
                && candidate.StructureInstance.OwnedByPlayerId == playerId
                && candidate.StructureInstance.RemovedOnTurnId == null)
            .Select(candidate => new CandidateRow(candidate.Id, candidate.StructureInstance.CampaignMapTileId))
            .ToListAsync(cancelToken);

        if (candidates.Count == 0)
        {
            logger.LogInformation("Player Id {PlayerId} has nothing above a camp standing in Game Id {GameId}, so the new camp feeds nobody and banks for itself.",
                playerId, gameId);

            return ServiceResult<int?>.Succeed(null);
        }

        var geometry = new CampaignMapGeometry(
            mapShape.CampaignMapOffsetId,
            mapShape.Width,
            mapShape.Height,
            mapShape.WrapsHorizontally,
            mapShape.WrapsVertically);

        Coord campCoord = geometry.GetCoord(campTileId.Value);

        // Nearest wins, and the lower settlement id breaks a tie. The settler-origin tiebreaker the design calls for sits between the two and is not applied: nothing
        // records where a Force was raised - see ICampParentResolver.
        ScoredCandidate parent = candidates
            .Select(candidate => new ScoredCandidate(candidate.SettlementId, geometry.GetDistance(campCoord, geometry.GetCoord(candidate.CampaignMapTileId))))
            .OrderBy(scored => scored.Distance)
            .ThenBy(scored => scored.SettlementId)
            .First();

        logger.LogInformation("Player Id {PlayerId}'s new camp in Game Id {GameId} will feed Settlement Id {ParentSettlementId}, {Distance} tile(s) away.",
            playerId, gameId, parent.SettlementId, parent.Distance);

        return ServiceResult<int?>.Succeed(parent.SettlementId);
    }

    /// <summary>The board's shape, which is all a distance needs: where the tile ids start, how big the map is, and which edges join.</summary>
    private sealed record MapShapeRow(int CampaignMapOffsetId, int Width, int Height, bool WrapsHorizontally, bool WrapsVertically);

    private sealed record CandidateRow(int SettlementId, int CampaignMapTileId);

    private sealed record ScoredCandidate(int SettlementId, int Distance);
}
