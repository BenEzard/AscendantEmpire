using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <inheritdoc cref="ICampaignRiverReader"/>
internal class CampaignRiverReader : ICampaignRiverReader
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Coord, TileRivers>> GetTileRiversAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<Coord> coords,
        CampaignMapGeometry geometry,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(coords);
        ArgumentNullException.ThrowIfNull(geometry);

        if (coords.Count == 0)
        {
            return new Dictionary<Coord, TileRivers>();
        }

        // The tiles asked about, plus the ones above and to the left of them, which own their North and West edges.
        IReadOnlyCollection<Coord> coordsToLoad = TileRiverDerivation.GetCoordsToLoad(coords, geometry);

        // A whole-map read wants every row anyway, so it is filtered on the Game alone. Listing all 40,000 ids of a large map instead would send the entire
        // key set as a parameter for a predicate that excludes nothing.
        bool isWholeMap = coordsToLoad.Count >= geometry.Width * geometry.Height;

        // Otherwise coordinates are turned into world-map ids rather than filtered on X and Y, so the query stays a single keyed lookup. A plain Contains
        // reaches SQL Server as one OPENJSON parameter, so this is one round-trip however many tiles it covers - and the in-memory provider can run it.
        List<int> campaignMapIds = isWholeMap ? [] : [.. coordsToLoad.Select(geometry.GetCampaignMapId)];

        List<RiverColumns> rows = await context.CampaignMaps
            .AsNoTracking()
            .Where(tile => tile.GameId == gameId && (isWholeMap || campaignMapIds.Contains(tile.CampaignMapId)))
            .Select(tile => new RiverColumns
            {
                XCoord = tile.XCoord,
                YCoord = tile.YCoord,
                SouthRiver = tile.SouthRiver,
                EastRiver = tile.EastRiver
            })
            .ToListAsync(cancelToken);

        // Keyed off the row's own coordinates rather than worked back out of its id, so a map whose ids and coordinates ever disagreed would not silently
        // place a river on the wrong tile.
        Dictionary<Coord, TileRiverEdges> ownedEdgesByCoord = rows.ToDictionary(
            row => new Coord(row.XCoord, row.YCoord),
            row => new TileRiverEdges(row.SouthRiver, row.EastRiver));

        // Only the tiles the caller asked about are described. The neighbours loaded above contributed their edges and are otherwise not reported, which is
        // what keeps an unexplored tile from disclosing anything while its explored neighbour still shows the edge they share.
        Dictionary<Coord, TileRivers> riversByCoord = [];

        foreach (Coord coord in coords)
        {
            riversByCoord[coord] = TileRiverDerivation.Derive(coord, ownedEdgesByCoord, geometry);
        }

        return riversByCoord;
    }

    /// <summary>
    /// The river columns of one world-map tile, with the coordinates to key them by.
    /// </summary>
    /// <remarks>Projected rather than materialising <c>CampaignMap</c>, so a whole-map read pulls four columns a row instead of the entire tile.</remarks>
    private sealed record RiverColumns
    {
        public required int XCoord { get; init; }
        public required int YCoord { get; init; }
        public required RiverWidth SouthRiver { get; init; }
        public required RiverWidth EastRiver { get; init; }
    }
}
