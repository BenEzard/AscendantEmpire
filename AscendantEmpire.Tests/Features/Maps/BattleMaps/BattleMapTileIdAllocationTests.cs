using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.BattleMaps;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Tests.Features.Maps.BattleMaps;

/// <summary>
/// Integration tests for how battle map tiles are numbered.
/// </summary>
/// <remarks>
/// <para>
/// <c>battle.BattleMap.Id</c> is not an IDENTITY column: tile (x, y) lives at <c>BattleMapOffsetId + (y * Size + x)</c>, so the application picks the ids and has to
/// reserve a block before writing them. The reservation reads the highest existing id under an exclusive table lock, and that lock is only worth anything if it is
/// still held when the rows claiming the range are inserted — it used to be taken on its own connection with no transaction, so it was released immediately.
/// </para>
/// <para>
/// The concurrency itself is not what these tests pin down; two racing generations are not something a test can reliably provoke. What they do check is that a
/// second battle map is numbered from where the first one ended, which is the observable behaviour the reservation exists to produce.
/// </para>
/// <para>
/// Runs at the SqlServer tier: the handler bulk-copies its tiles and takes a real transaction, neither of which an in-memory provider can do.
/// </para>
/// </remarks>
[TestFixture]
internal class BattleMapTileIdAllocationTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 3;

    /// <summary>The smallest the request validator allows, so each generated map is 900 tiles rather than 2,500.</summary>
    private const int _BATTLE_MAP_SIZE = 30;

    private IMediator _mediator = null!;

    [SetUp]
    public void SetUpMediator()
    {
        // GenerateBattleMapHandler takes a context directly rather than a factory.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddMediator(typeof(GetPlayersHandler).Assembly)
            .AddLogging()
            .BuildServiceProvider();

        _mediator = provider.GetRequiredService<IMediator>();
    }

    /// <summary>
    /// Sets a game up and gives its gallery map the master seed the battle map generator needs. The shared fixture seeds PropertiesJson as an empty object, which
    /// is enough for the campaign map but makes battle generation fail with "Master seed not found".
    /// </summary>
    private async Task SeedFixtureAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        await using AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync();

        CampaignMapGalleryIndex galleryIndex = await seed.CampaignMapGalleryIndex.SingleAsync(cmi => cmi.Id == _galleryIndexId);
        galleryIndex.PropertiesJson = """{"seed":"123456789"}""";

        await seed.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a game on the seeded gallery map. The generation seed is fixed, so two games created this way differ in nothing the battle map generator reads.
    /// </summary>
    private async Task<int> CreateGameAsync(string gameName)
    {
        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: gameName, NumberOfPlayers: 2, BattleMapSize: _BATTLE_MAP_SIZE, GenerationSeed: 42),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    private async Task<int> SetupGameWithSeededGalleryAsync()
    {
        await SeedFixtureAsync();

        return await CreateGameAsync("Battle Map Game");
    }

    private async Task<int> GenerateBattleMapAsync(int gameId, int campaignMapId)
    {
        ServiceResultBase.ServiceResult<int> result = await _mediator.Send(new GenerateBattleMapRequest(gameId, campaignMapId));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    [Test, Description("A generated battle map's tiles are numbered row-major from the offset recorded on its index row.")]
    public async Task BattleMapTilesAreRowMajorFromTheRecordedOffsetAsync()
    {
        int gameId = await SetupGameWithSeededGalleryAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMap campaignTile = await context.CampaignMaps.Where(cm => cm.GameId == gameId).OrderBy(cm => cm.CampaignMapId).FirstAsync();

        int battleMapIndexId = await GenerateBattleMapAsync(gameId, campaignTile.CampaignMapId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        BattleMapIndex index = await verify.BattleMapIndex.SingleAsync(bmi => bmi.Id == battleMapIndexId);
        List<BattleMap> tiles = await verify.BattleMap.Where(bm => bm.BattleMapIndexId == battleMapIndexId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tiles, Has.Count.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE));
            Assert.That(index.GenerationStatus, Is.EqualTo(MapCreationStatus.Complete));
            Assert.That(index.BattleMapOffsetId, Is.EqualTo(tiles.Min(t => t.Id)), "The recorded offset is the first tile written.");

            foreach (BattleMap tile in tiles)
            {
                int expectedId = index.BattleMapOffsetId + (tile.YCoord * _BATTLE_MAP_SIZE) + tile.XCoord;

                Assert.That(tile.Id, Is.EqualTo(expectedId), $"Battle tile ({tile.XCoord}, {tile.YCoord}) should be at offset + y * size + x.");
            }
        }
    }

    [Test, Description("A second battle map is numbered from where the first one ended, so the two blocks neither overlap nor leave a gap.")]
    public async Task SecondBattleMapIsNumberedAfterTheFirstAsync()
    {
        const int tilesPerBattleMap = _BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE;

        int gameId = await SetupGameWithSeededGalleryAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        List<CampaignMap> campaignTiles = await context.CampaignMaps.Where(cm => cm.GameId == gameId).OrderBy(cm => cm.CampaignMapId).Take(2).ToListAsync();

        int firstIndexId = await GenerateBattleMapAsync(gameId, campaignTiles[0].CampaignMapId);
        int secondIndexId = await GenerateBattleMapAsync(gameId, campaignTiles[1].CampaignMapId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        BattleMapIndex firstIndex = await verify.BattleMapIndex.SingleAsync(bmi => bmi.Id == firstIndexId);
        BattleMapIndex secondIndex = await verify.BattleMapIndex.SingleAsync(bmi => bmi.Id == secondIndexId);

        int[] firstTileIds = await verify.BattleMap.Where(bm => bm.BattleMapIndexId == firstIndexId).Select(bm => bm.Id).ToArrayAsync();
        int[] secondTileIds = await verify.BattleMap.Where(bm => bm.BattleMapIndexId == secondIndexId).Select(bm => bm.Id).ToArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstTileIds.Intersect(secondTileIds), Is.Empty, "The two battle maps share no tile ids.");

            Assert.That(secondIndex.BattleMapOffsetId - firstIndex.BattleMapOffsetId, Is.EqualTo(tilesPerBattleMap),
                "The second reservation starts immediately after the first map's tiles, so it saw them.");

            Assert.That(secondIndex.BattleMapOffsetId, Is.EqualTo(firstTileIds.Max() + 1));
        }
    }

    [Test, Description("Generation reads the map dimensions from the game, so editing the shared gallery row afterwards does not change the terrain it produces.")]
    public async Task GenerationIgnoresLaterEditsToTheSharedGalleryRowAsync()
    {
        await SeedFixtureAsync();

        // Two games off the same gallery map, with the same generation seed, so everything the generator reads is identical between them.
        int firstGameId = await CreateGameAsync("Before The Edit");
        int secondGameId = await CreateGameAsync("After The Edit");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMap firstTile = await context.CampaignMaps.SingleAsync(cm => cm.GameId == firstGameId && cm.XCoord == 2 && cm.YCoord == 1);
        CampaignMap secondTile = await context.CampaignMaps.SingleAsync(cm => cm.GameId == secondGameId && cm.XCoord == 2 && cm.YCoord == 1);

        int firstIndexId = await GenerateBattleMapAsync(firstGameId, firstTile.CampaignMapId);

        // Shrink the gallery map to a single tile, standing in for a gallery row edited after both games were copied from it. Both games still describe a 4x3 map,
        // so the second generation must produce exactly the terrain the first did. Reading the dimensions from the gallery would feed the generator a different
        // campaign size and a different set of neighbours, and the altitudes would diverge.
        await using (AscendantEmpireContext edit = await DbFactory.CreateDbContextAsync())
        {
            CampaignMapGalleryIndex galleryIndex = await edit.CampaignMapGalleryIndex.SingleAsync(cmi => cmi.Id == _galleryIndexId);
            galleryIndex.Width = 1;
            galleryIndex.Height = 1;

            await edit.SaveChangesAsync();
        }

        int secondIndexId = await GenerateBattleMapAsync(secondGameId, secondTile.CampaignMapId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<BattleMap> firstTiles = await verify.BattleMap.Where(bm => bm.BattleMapIndexId == firstIndexId).OrderBy(bm => bm.Id).ToListAsync();
        List<BattleMap> secondTiles = await verify.BattleMap.Where(bm => bm.BattleMapIndexId == secondIndexId).OrderBy(bm => bm.Id).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstTiles, Has.Count.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE));
            Assert.That(secondTiles, Has.Count.EqualTo(firstTiles.Count));

            Assert.That(secondTiles.Select(t => t.AltitudeLevel), Is.EqualTo(firstTiles.Select(t => t.AltitudeLevel)).AsCollection,
                "The edit to the gallery row must not have reached the generated terrain.");
        }
    }

    [Test, Description("Asking for a battle map that already exists returns the existing one rather than numbering a second copy of it.")]
    public async Task RegeneratingAnExistingBattleMapDoesNotAllocateAgainAsync()
    {
        int gameId = await SetupGameWithSeededGalleryAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMap campaignTile = await context.CampaignMaps.Where(cm => cm.GameId == gameId).OrderBy(cm => cm.CampaignMapId).FirstAsync();

        int firstIndexId = await GenerateBattleMapAsync(gameId, campaignTile.CampaignMapId);
        int secondIndexId = await GenerateBattleMapAsync(gameId, campaignTile.CampaignMapId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int totalTiles = await verify.BattleMap.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondIndexId, Is.EqualTo(firstIndexId), "The existing battle map is returned as-is.");
            Assert.That(totalTiles, Is.EqualTo(_BATTLE_MAP_SIZE * _BATTLE_MAP_SIZE), "No second set of tiles was written.");
        }
    }
}
