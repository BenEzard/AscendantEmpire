using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.Tests.Features.Maps.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Pins what <see cref="RegenerateMapResources"/> and <see cref="MapResourceRegenerationWriter"/> leave in the database at the end of a Round.
/// </summary>
/// <remarks>
/// <para>
/// Containerised: the write deletes with <c>ExecuteDeleteAsync</c> inside a transaction, which <c>testing.md</c> says the in-memory provider cannot run. The rules
/// themselves are pinned without a database in <c>MapResourceRegenerationRulesTests</c> and <c>MapResourceSpreadRulesTests</c>.
/// </para>
/// <para>
/// The map is a 5×5 Forest that does not wrap. The spreader stands in the middle and everything else in a corner, so a spread in any direction reaches an empty tile
/// and never one another test instance stands on.
/// </para>
/// </remarks>
[TestFixture]
internal sealed class RegenerateMapResourcesTests : SqlServerTestBase
{
    private const int _MAP_SIZE = 5;
    private const int _OFFSET = 1000;
    private const int _ROUND = 20;
    private const long _BERRY_CEILING = 500_000;
    private const int _BERRY_SURVIVAL_ROUNDS = 5;

    private int _gameId;
    private int _playerId;
    private MapResourceTestCatalogue _catalogue = null!;

    [SetUp]
    public async Task SeedGameAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _catalogue = await MapResourceTestSeed.SeedCatalogueAsync(context);
        await MapResourceTestSeed.EnsureGeographyAsync(context, CampaignMapGeographyType.Flat);

        // Every spread succeeds and no dormant Berry ever comes back, so the outcome does not hang on a roll.
        MapResourceType berry = await context.MapResourceTypes.Include(type => type.Habitats).SingleAsync(type => type.Id == _catalogue.BerryTypeId);
        berry.ReseedLikelihood = 0m;
        berry.Habitats.Single().SpreadLikelihood = 1m;

        var game = new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = "Regeneration Game",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 20261119,
            BattleMapSize = TestGameSettings.MINIMUM_BATTLE_MAP_SIZE,
            MapWidth = _MAP_SIZE,
            MapHeight = _MAP_SIZE,
            CampaignMapOffsetId = _OFFSET,
        };

        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        var player = new Player
        {
            GameId = game.Id,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Ruler",
            FactionName = "Faction",
            PeopleName = "People",
            Demonym = "Person",
            Language = "Language",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = PlayerStatus.Active,
            CampaignOffsetId = 1,
        };

        context.Players.Add(player);

        var forest = await context.CampaignBiomes.SingleAsync(biome => biome.BiomeTypeId == BiomeType.Forest);
        var flat = await context.CampaignGeographies.SingleAsync(geography => geography.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat);

        for (int y = 0; y < _MAP_SIZE; y++)
        {
            for (int x = 0; x < _MAP_SIZE; x++)
            {
                context.CampaignMaps.Add(new CampaignMap
                {
                    CampaignMapId = TileId(x, y),
                    GameId = game.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = BiomeType.Forest,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    Biome = forest,
                    Geography = flat,
                    Game = game,
                    FirstVisitor = null,
                    LastVisitor = null,
                    OwnedBy = null
                });
            }
        }

        await context.SaveChangesAsync();

        _gameId = game.Id;
        _playerId = player.Id;
    }

    #region The process
    [Test, Description("A Berry at its ceiling spreads into an empty neighbour, which starts at the spreader's growth share, inherits its reduced limit and its discoverers.")]
    public async Task ShouldCreateADiscoveredInstanceWhenABerrySpreadsAsync()
    {
        MapResourceInstance spreader = await AddBerryAsync(x: 2, y: 2, quantity: _BERRY_CEILING, spreadLimit: 2, discovered: true);

        await RunProcessAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceInstance created = await context.MapResourceInstances.SingleAsync(instance => instance.Id != spreader.Id);
        MapResourceInstance spreaderAfter = await context.MapResourceInstances.SingleAsync(instance => instance.Id == spreader.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Quantity, Is.EqualTo(MapResourceRegenerationRules.GrowthAmount(_BERRY_CEILING, 0.1m)));
            Assert.That(created.SpreadLimit, Is.EqualTo(1));
            Assert.That(created.CreatedOnRound, Is.EqualTo(_ROUND));
            Assert.That(created.EstimationFuzzyFactor, Is.Not.Null);
            Assert.That(spreaderAfter.SpreadLimit, Is.EqualTo(1));
            Assert.That(await context.PlayerKnownMapResources.AnyAsync(known => known.MapResourceInstanceId == created.Id && known.PlayerId == _playerId && known.DiscoveredOnRound == _ROUND), Is.True);
        }
    }

    [Test, Description("A Berry below its ceiling grows by its growth percentage, and one just exhausted is stamped dormant rather than deleted.")]
    public async Task ShouldGrowAndStampDormancyAsync()
    {
        MapResourceInstance growing = await AddBerryAsync(x: 0, y: 4, quantity: 200_000, spreadLimit: 2, discovered: false);
        MapResourceInstance exhausted = await AddBerryAsync(x: 4, y: 0, quantity: 0, spreadLimit: 2, discovered: false);

        await RunProcessAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await context.MapResourceInstances.SingleAsync(instance => instance.Id == growing.Id)).Quantity, Is.EqualTo(220_000));
            Assert.That((await context.MapResourceInstances.SingleAsync(instance => instance.Id == exhausted.Id)).DormantSinceRound, Is.EqualTo(_ROUND));
        }
    }

    [Test, Description("A dormant Berry that has failed every survival Round is deleted for good, with what the Players knew of it.")]
    public async Task ShouldDeleteAnInstanceThatOutlivedItsSurvivalAsync()
    {
        MapResourceInstance dormant = await AddBerryAsync(x: 0, y: 0, quantity: 0, spreadLimit: 2, discovered: true, dormantSinceRound: _ROUND - _BERRY_SURVIVAL_ROUNDS);

        await RunProcessAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.MapResourceInstances.AnyAsync(instance => instance.Id == dormant.Id), Is.False);
            Assert.That(await context.PlayerKnownMapResources.AnyAsync(known => known.MapResourceInstanceId == dormant.Id), Is.False);
        }
    }

    [Test, Description("A Distribution grows its own amount rows, since it holds no Quantity.")]
    public async Task ShouldGrowADistributionsAmountsAsync()
    {
        int woodId = await AddWoodAsync(x: 4, y: 4, amount: 10_000_000);

        await RunProcessAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceInstanceAmount amount = await context.MapResourceInstanceAmounts.SingleAsync(row => row.MapResourceInstanceId == woodId);

        Assert.That(amount.Amount, Is.EqualTo(10_500_000));
    }
    #endregion

    #region The writer
    [Test, Description("A column going back to null is written, which a stub update would miss if it relied on change detection against the stub's own nulls.")]
    public async Task ShouldClearDormancyAndLeaveUnwrittenColumnsAloneAsync()
    {
        MapResourceInstance revived = await AddBerryAsync(x: 0, y: 0, quantity: 0, spreadLimit: 0, discovered: false, dormantSinceRound: _ROUND - 1);

        await WriteAsync(new MapResourceRegenerationPlan
        {
            InstanceUpdates = [new RegenerationInstanceUpdate { MapResourceInstanceId = revived.Id, Quantity = 50_000, SpreadLimit = 1, DormantSinceRound = null }]
        });

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceInstance after = await context.MapResourceInstances.SingleAsync(instance => instance.Id == revived.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Quantity, Is.EqualTo(50_000));
            Assert.That(after.SpreadLimit, Is.EqualTo(1));
            Assert.That(after.DormantSinceRound, Is.Null);
            Assert.That(after.EstimationFuzzyFactor, Is.EqualTo(revived.EstimationFuzzyFactor));
            Assert.That(after.MapResourceTypeId, Is.EqualTo(_catalogue.BerryTypeId));
        }
    }

    [Test, Description("A deleted Distribution takes its amount rows with it, since the foreign keys do not cascade.")]
    public async Task ShouldDeleteADistributionsAmountsWithItAsync()
    {
        int woodId = await AddWoodAsync(x: 4, y: 4, amount: 0);

        await WriteAsync(new MapResourceRegenerationPlan { DeletedMapResourceInstanceIds = [woodId] });

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.MapResourceInstances.AnyAsync(instance => instance.Id == woodId), Is.False);
            Assert.That(await context.MapResourceInstanceAmounts.AnyAsync(amount => amount.MapResourceInstanceId == woodId), Is.False);
        }
    }
    #endregion

    private async Task RunProcessAsync()
    {
        var process = new RegenerateMapResources(
            DbFactory,
            new MapResourceRegenerationReader(),
            new MapResourceRegenerationWriter(),
            new SightDataReader(),
            NullLogger<RegenerateMapResources>.Instance);

        string result = await process.ProcessAsync(new TurnPipelineContextData(TurnPipelineType.EndCampaignRound, _gameId, _ROUND));

        Assert.That(result, Is.Empty);
    }

    private async Task WriteAsync(MapResourceRegenerationPlan plan)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await new MapResourceRegenerationWriter().WriteAsync(context, _gameId, _ROUND, plan);
    }

    private async Task<MapResourceInstance> AddBerryAsync(int x, int y, long quantity, int spreadLimit, bool discovered, int? dormantSinceRound = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var berry = new MapResourceInstance
        {
            GameId = _gameId,
            CampaignMapId = TileId(x, y),
            MapResourceTypeId = _catalogue.BerryTypeId,
            Slot = 1,
            Quantity = quantity,
            SpreadLimit = spreadLimit,
            DormantSinceRound = dormantSinceRound,
            EstimationFuzzyFactor = 0.2m,
            CreatedOnRound = 0
        };

        context.MapResourceInstances.Add(berry);
        await context.SaveChangesAsync();

        if (discovered)
        {
            context.PlayerKnownMapResources.Add(new PlayerKnownMapResource { GameId = _gameId, PlayerId = _playerId, MapResourceInstanceId = berry.Id, DiscoveredOnRound = 0 });
            await context.SaveChangesAsync();
        }

        return berry;
    }

    private async Task<int> AddWoodAsync(int x, int y, long amount)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var wood = new MapResourceInstance
        {
            GameId = _gameId,
            CampaignMapId = TileId(x, y),
            MapResourceTypeId = _catalogue.WoodTypeId,
            Slot = 1,
            EstimationFuzzyFactor = 0.1m,
            MapResourceDistributionScenarioId = _catalogue.ScenarioId,
            CreatedOnRound = 0
        };

        context.MapResourceInstances.Add(wood);
        await context.SaveChangesAsync();

        context.MapResourceInstanceAmounts.Add(new MapResourceInstanceAmount { MapResourceInstanceId = wood.Id, AssetId = _catalogue.AssetId, GameId = _gameId, Amount = amount });
        await context.SaveChangesAsync();

        return wood.Id;
    }

    private static int TileId(int x, int y)
    {
        return _OFFSET + x + (y * _MAP_SIZE);
    }
}
