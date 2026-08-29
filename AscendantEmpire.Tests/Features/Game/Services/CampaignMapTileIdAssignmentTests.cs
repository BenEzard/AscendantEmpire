using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Integration tests for how game setup numbers campaign map tiles.
/// </summary>
/// <remarks>
/// <para>
/// The map code addresses a tile as <c>offset + XCoord + YCoord * MapWidth</c>. These tests pin that
/// relationship down at the point the rows are written, because it used to be left to an IDENTITY
/// column: the database numbered rows in whatever order the query plan produced them, which for
/// PlayerCampaignMap turned out to be column-major, and every read silently resolved the transposed
/// tile.
/// </para>
/// <para>
/// Every map here is deliberately non-square. On a square map row-major and column-major numbering
/// agree along the diagonal and differ everywhere else, but a bug that transposes the axes would
/// still produce a full set of valid-looking ids; on a 4x3 map the two orderings cannot be confused.
/// </para>
/// <para>
/// Runs at the SqlServer tier because <c>SetupGameAsync</c> writes both maps with raw SQL, which an
/// in-memory provider cannot execute.
/// </para>
/// </remarks>
internal class CampaignMapTileIdAssignmentTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 3;
    private const int _TILES_PER_MAP = _MAP_WIDTH * _MAP_HEIGHT;

    private SetupGameRequest BuildRequest(string gameName, int numberOfPlayers)
    {
        return new SetupGameRequest(
            GalleryMapId: _galleryIndexId,
            GameName: gameName,
            NumberOfPlayers: numberOfPlayers,
            BattleMapSize: 50,
            GenerationSeed: null);
    }

    private async Task<int> SetupGameAsync(string gameName, int numberOfPlayers)
    {
        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(gameName, numberOfPlayers), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    [Test, Description("Game setup records the map's offset and dimensions on the game itself, rather than leaving them to be read back from the shared gallery row.")]
    public async Task SetupSnapshotsMapOffsetAndDimensionsOntoTheGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        int gameId = await SetupGameAsync("Offset Snapshot Game", numberOfPlayers: 2);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameIndex game = await verify.GameIndex.SingleAsync(g => g.Id == gameId);
        int lowestTileId = await verify.CampaignMaps.Where(cm => cm.GameId == gameId).MinAsync(cm => cm.CampaignMapId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.MapWidth, Is.EqualTo(_MAP_WIDTH));
            Assert.That(game.MapHeight, Is.EqualTo(_MAP_HEIGHT));
            Assert.That(game.CampaignMapOffsetId, Is.EqualTo(lowestTileId), "The offset is the id of the tile at (0, 0), which is the lowest in the block.");
        }
    }

    [Test, Description("World map tile ids are laid out row-major from the game's recorded offset.")]
    public async Task CampaignMapTileIdsAreRowMajorFromTheGameOffsetAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        int gameId = await SetupGameAsync("Row Major Game", numberOfPlayers: 2);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameIndex game = await verify.GameIndex.SingleAsync(g => g.Id == gameId);
        List<CampaignMap> tiles = await verify.CampaignMaps.Where(cm => cm.GameId == gameId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tiles, Has.Count.EqualTo(_TILES_PER_MAP));

            foreach (CampaignMap tile in tiles)
            {
                int expectedTileId = game.CampaignMapOffsetId + tile.XCoord + (tile.YCoord * game.MapWidth);

                Assert.That(tile.CampaignMapId, Is.EqualTo(expectedTileId),
                    $"Tile ({tile.XCoord}, {tile.YCoord}) should be at offset + X + Y * width.");
            }
        }
    }

    [Test, Description("Each player's map is a contiguous row-major block starting at that player's own offset, and the blocks do not overlap.")]
    public async Task PlayerCampaignMapTileIdsAreRowMajorPerPlayerBlockAsync()
    {
        const int numberOfPlayers = 3;

        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: numberOfPlayers);

        int gameId = await SetupGameAsync("Player Block Game", numberOfPlayers: numberOfPlayers);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameIndex game = await verify.GameIndex.SingleAsync(g => g.Id == gameId);
        List<Player> players = await verify.Players.Where(p => p.GameId == gameId).ToListAsync();
        List<PlayerCampaignMap> playerTiles = await verify.PlayerCampaignMaps.Where(pcm => pcm.GameId == gameId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(players, Has.Count.EqualTo(numberOfPlayers));
            Assert.That(playerTiles, Has.Count.EqualTo(numberOfPlayers * _TILES_PER_MAP));

            foreach (Player player in players)
            {
                List<PlayerCampaignMap> tilesForPlayer = [.. playerTiles.Where(pcm => pcm.PlayerId == player.Id)];

                Assert.That(tilesForPlayer, Has.Count.EqualTo(_TILES_PER_MAP), $"Player {player.Id} sees the whole map.");

                foreach (PlayerCampaignMap tile in tilesForPlayer)
                {
                    int expectedTileId = player.CampaignOffsetId + tile.XCoord + (tile.YCoord * game.MapWidth);

                    Assert.That(tile.PlayerCampaignMapId, Is.EqualTo(expectedTileId),
                        $"Player {player.Id} tile ({tile.XCoord}, {tile.YCoord}) should be at that player's offset + X + Y * width.");
                }
            }

            // Distinct, non-overlapping blocks: every player starts a full map-width past the previous one.
            int[] offsets = [.. players.Select(p => p.CampaignOffsetId).Order()];

            Assert.That(offsets, Is.Unique, "Each player gets their own block.");

            for (int playerIndex = 1; playerIndex < offsets.Length; playerIndex++)
            {
                Assert.That(offsets[playerIndex] - offsets[playerIndex - 1], Is.EqualTo(_TILES_PER_MAP),
                    "Player blocks are contiguous and exactly one map apart.");
            }
        }
    }

    [Test, Description("A player's tile and the world tile beneath it sit at the same position within their blocks, which is what lets the map read path translate between them.")]
    public async Task PlayerAndWorldTilesShareTheSameOffsetWithinTheirBlocksAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        int gameId = await SetupGameAsync("Translation Game", numberOfPlayers: 2);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameIndex game = await verify.GameIndex.SingleAsync(g => g.Id == gameId);
        List<Player> players = await verify.Players.Where(p => p.GameId == gameId).ToListAsync();
        List<PlayerCampaignMap> playerTiles = await verify.PlayerCampaignMaps.Where(pcm => pcm.GameId == gameId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            foreach (PlayerCampaignMap playerTile in playerTiles)
            {
                Player owner = players.Single(p => p.Id == playerTile.PlayerId);

                int positionInPlayerBlock = playerTile.PlayerCampaignMapId - owner.CampaignOffsetId;
                int positionInWorldBlock = playerTile.CampaignMapId - game.CampaignMapOffsetId;

                Assert.That(positionInPlayerBlock, Is.EqualTo(positionInWorldBlock),
                    $"Player {owner.Id} tile ({playerTile.XCoord}, {playerTile.YCoord}) must occupy the same slot in both blocks.");
            }
        }
    }

    [Test, Description("A second game is numbered from its own offset, well past id 1 — the case that broke, because the very first game's map happens to start at 1.")]
    public async Task SecondGameIsNumberedFromItsOwnOffsetAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        int firstGameId = await SetupGameAsync("First Game", numberOfPlayers: 2);
        int secondGameId = await SetupGameAsync("Second Game", numberOfPlayers: 2);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameIndex firstGame = await verify.GameIndex.SingleAsync(g => g.Id == firstGameId);
        GameIndex secondGame = await verify.GameIndex.SingleAsync(g => g.Id == secondGameId);

        List<CampaignMap> firstGameTiles = await verify.CampaignMaps.Where(cm => cm.GameId == firstGameId).ToListAsync();
        List<CampaignMap> secondGameTiles = await verify.CampaignMaps.Where(cm => cm.GameId == secondGameId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondGame.CampaignMapOffsetId, Is.GreaterThan(firstGame.CampaignMapOffsetId),
                "The second game's block starts after the first game's.");

            Assert.That(secondGame.CampaignMapOffsetId - firstGame.CampaignMapOffsetId, Is.EqualTo(_TILES_PER_MAP),
                "Blocks are contiguous, so the second starts exactly one map after the first.");

            // The whole point: the second game's ids are nowhere near 1, and the formula still holds.
            foreach (CampaignMap tile in secondGameTiles)
            {
                int expectedTileId = secondGame.CampaignMapOffsetId + tile.XCoord + (tile.YCoord * secondGame.MapWidth);

                Assert.That(tile.CampaignMapId, Is.EqualTo(expectedTileId));
            }

            int[] firstGameTileIds = [.. firstGameTiles.Select(cm => cm.CampaignMapId)];
            int[] secondGameTileIds = [.. secondGameTiles.Select(cm => cm.CampaignMapId)];

            Assert.That(firstGameTileIds.Intersect(secondGameTileIds), Is.Empty, "The two games share no tile ids.");
        }
    }

    [Test, Description("Starting troops are placed on real world-map tile rows, not on ids derived from coordinates with a hardcoded offset.")]
    public async Task StarterTroopsReferenceRealWorldMapTilesAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        // Set a first game up so the game under test is not the one whose map starts at id 1.
        await SetupGameAsync("Throwaway First Game", numberOfPlayers: 2);
        int gameId = await SetupGameAsync("Troop Placement Game", numberOfPlayers: 2);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameIndex game = await verify.GameIndex.SingleAsync(g => g.Id == gameId);

        List<int> troopTileIds = await verify.TroopInstances
            .Where(t => t.GameId == gameId)
            .Select(t => t.CampaignMapTileId)
            .ToListAsync();

        List<int> tileIdsForGame = await verify.CampaignMaps
            .Where(cm => cm.GameId == gameId)
            .Select(cm => cm.CampaignMapId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(troopTileIds, Is.Not.Empty, "Setup spawns starting units.");

            // Distinct, because each player's settler and scout share a start tile and Is.SubsetOf counts duplicates.
            Assert.That(troopTileIds.Distinct(), Is.SubsetOf(tileIdsForGame), "Every troop stands on a tile belonging to its own game.");

            Assert.That(troopTileIds, Has.All.GreaterThanOrEqualTo(game.CampaignMapOffsetId),
                "No troop is left on an id from an earlier game's block.");
        }
    }
}
