using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="CampaignRiverReader"/>: that the world map's river columns are read off the right rows and reported for the right tiles.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The reader is one projected LINQ query with no raw SQL, no bulk update and no complex type in its shape, so nothing here needs
/// a container.
/// </para>
/// <para>
/// Two Games are seeded on 4 x 3 maps that wrap on neither axis. Game 1 carries a river down the middle: a Medium river along the South edge of (1, 0)
/// and a Large one along the East edge of (1, 1). Game 2 exists only so that reading one Game can be shown not to pick up the other's rivers.
/// </para>
/// <para>
/// What fog of war does with the answer is not tested here, because the reader does not know about it: it describes the tiles it is handed and no others,
/// which is what lets the map read ask about explored ground alone. <c>TileRiverDerivationTests</c> pins the rule that an edge is still reported when only
/// one of the two tiles it separates is being described.
/// </para>
/// </remarks>
[TestFixture]
internal class CampaignRiverReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;

    private const int _MAP_OFFSET = 100;
    private const int _OTHER_MAP_OFFSET = 500;

    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 3;

    private readonly CampaignRiverReader _reader = new();

    /// <summary>The geometry of the Game under test: a 4 x 3 map that joins on neither axis.</summary>
    private static CampaignMapGeometry Geometry()
    {
        return new CampaignMapGeometry(_MAP_OFFSET, _MAP_WIDTH, _MAP_HEIGHT, WrapsHorizontally: false, WrapsVertically: false);
    }

    [SetUp]
    public async Task SeedMapsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var grassland = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MovementCost = 1m,
            IsTraversable = true,
            MapEditorHtmlColor = "#7cb342"
        };

        var flat = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        context.CampaignBiomes.Add(grassland);
        context.CampaignGeographies.Add(flat);

        context.GameIndex.AddRange(NewGame(_GAME_ID, _MAP_OFFSET), NewGame(_OTHER_GAME_ID, _OTHER_MAP_OFFSET));

        for (int y = 0; y < _MAP_HEIGHT; y++)
        {
            for (int x = 0; x < _MAP_WIDTH; x++)
            {
                // Game 1's river: one Medium edge between (1, 0) and (1, 1), and one Large edge between (1, 1) and (2, 1).
                RiverWidth south = (x, y) == (1, 0) ? RiverWidth.Medium : RiverWidth.None;
                RiverWidth east = (x, y) == (1, 1) ? RiverWidth.Large : RiverWidth.None;

                context.CampaignMaps.Add(NewTile(_GAME_ID, _MAP_OFFSET, x, y, grassland, flat, south, east));

                // The other Game carries a river on every edge, so any leak across Games would show up as a river where this Game has none.
                context.CampaignMaps.Add(NewTile(_OTHER_GAME_ID, _OTHER_MAP_OFFSET, x, y, grassland, flat, RiverWidth.Large, RiverWidth.Large));
            }
        }

        await context.SaveChangesAsync();
    }

    private static GameIndex NewGame(int gameId, int mapOffset)
    {
        return new GameIndex
        {
            Id = gameId,
            GameName = $"River Game {gameId}",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = GameIndex.MIN_BATTLE_MAP_SIZE,
            CampaignMapOffsetId = mapOffset,
            MapWidth = _MAP_WIDTH,
            MapHeight = _MAP_HEIGHT,
            HorizontalWrap = false,
            VerticalWrap = false
        };
    }

    private static CampaignMap NewTile(
        int gameId,
        int mapOffset,
        int x,
        int y,
        CampaignBiome biome,
        CampaignGeography geography,
        RiverWidth southRiver,
        RiverWidth eastRiver)
    {
        return new CampaignMap
        {
            CampaignMapId = mapOffset + x + (y * _MAP_WIDTH),
            GameId = gameId,
            XCoord = x,
            YCoord = y,
            BiomeTypeId = biome.BiomeTypeId,
            CampaignMapGeographyTypeId = geography.CampaignMapGeographyTypeId,
            SouthRiver = southRiver,
            EastRiver = eastRiver,
            Biome = biome,
            Geography = geography,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        };
    }

    private async Task<IReadOnlyDictionary<Coord, TileRivers>> ReadAsync(params Coord[] coords)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _reader.GetTileRiversAsync(context, _GAME_ID, coords, Geometry(), CancellationToken.None);
    }

    [Test, Description("A tile's own SouthRiver and EastRiver columns are reported as its South and East edges.")]
    public async Task ReportsTheTilesOwnColumns()
    {
        IReadOnlyDictionary<Coord, TileRivers> rivers = await ReadAsync(new Coord(1, 0), new Coord(1, 1));

        Assert.Multiple(() =>
        {
            Assert.That(rivers[new Coord(1, 0)].South, Is.EqualTo(RiverWidth.Medium));
            Assert.That(rivers[new Coord(1, 1)].East, Is.EqualTo(RiverWidth.Large));
        });
    }

    [Test, Description("A tile's North and West edges are read off the neighbouring rows that own them, which are loaded even though they were not asked about.")]
    public async Task ReportsEdgesOwnedByNeighbouringRows()
    {
        // Only (1, 1) and (2, 1) are asked about. (1, 0) owns the Medium edge above (1, 1), and (1, 1) owns the Large edge west of (2, 1).
        IReadOnlyDictionary<Coord, TileRivers> rivers = await ReadAsync(new Coord(1, 1), new Coord(2, 1));

        Assert.Multiple(() =>
        {
            Assert.That(rivers[new Coord(1, 1)].North, Is.EqualTo(RiverWidth.Medium));
            Assert.That(rivers[new Coord(2, 1)].West, Is.EqualTo(RiverWidth.Large));
        });
    }

    [Test, Description("Only the tiles asked about come back, so the neighbours loaded for their columns disclose nothing of their own.")]
    public async Task DescribesOnlyTheTilesAskedAbout()
    {
        IReadOnlyDictionary<Coord, TileRivers> rivers = await ReadAsync(new Coord(1, 1));

        Assert.Multiple(() =>
        {
            Assert.That(rivers.Keys, Is.EquivalentTo(new[] { new Coord(1, 1) }));
            // (1, 0) was loaded because it owns the edge above (1, 1), and is still not described.
            Assert.That(rivers.ContainsKey(new Coord(1, 0)), Is.False);
        });
    }

    [Test, Description("A tile with no river on any edge is still described, so 'the ground here has no river' is distinguishable from an absent answer.")]
    public async Task ReportsATileWithNoRivers()
    {
        IReadOnlyDictionary<Coord, TileRivers> rivers = await ReadAsync(new Coord(3, 2));

        Assert.Multiple(() =>
        {
            Assert.That(rivers.ContainsKey(new Coord(3, 2)), Is.True);
            Assert.That(rivers[new Coord(3, 2)].HasAny, Is.False);
        });
    }

    [Test, Description("Rivers are read from the Game asked about, so another Game's map cannot contribute an edge.")]
    public async Task ReadsOnlyTheGameAskedAbout()
    {
        // The other Game has a Large river on every edge of every tile, and this tile has none in the Game under test.
        IReadOnlyDictionary<Coord, TileRivers> rivers = await ReadAsync(new Coord(3, 2));

        Assert.That(rivers[new Coord(3, 2)].HasAny, Is.False);
    }

    [Test, Description("Asking about every tile on the map still reports each one, since that read is filtered on the Game rather than on a list of ids.")]
    public async Task ReadsTheWholeMapInOneGo()
    {
        Coord[] everyTile = [.. Enumerable.Range(0, _MAP_HEIGHT).SelectMany(y => Enumerable.Range(0, _MAP_WIDTH).Select(x => new Coord(x, y)))];

        IReadOnlyDictionary<Coord, TileRivers> rivers = await ReadAsync(everyTile);

        Assert.Multiple(() =>
        {
            Assert.That(rivers, Has.Count.EqualTo(_MAP_WIDTH * _MAP_HEIGHT));
            Assert.That(rivers[new Coord(1, 1)].North, Is.EqualTo(RiverWidth.Medium));
            Assert.That(rivers[new Coord(1, 1)].East, Is.EqualTo(RiverWidth.Large));
        });
    }

    [Test, Description("Asking about no tiles at all reads nothing, rather than falling through to a query for the whole map.")]
    public async Task ReadsNothingForAnEmptyRequest()
    {
        IReadOnlyDictionary<Coord, TileRivers> rivers = await ReadAsync();

        Assert.That(rivers, Is.Empty);
    }
}
