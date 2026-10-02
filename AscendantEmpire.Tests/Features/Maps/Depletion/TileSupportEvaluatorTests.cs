using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="TileSupportEvaluator"/>: which Forces make up a tile's need, and that the level comes from the same pool the end-of-Turn feed eats from.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The evaluator's reads are LINQ only. The Player's values and the biomes' regeneration values come from the real readers. The forecast itself is
/// pinned without a database in <c>TileSupportForecasterTests</c>.
/// </para>
/// <para>
/// The catalogue has Grassland (Forage 400, Hunt 200: 600 food on land), Ocean (Fish 300 only), neither with a Regeneration Percent so their Turns are plain division, and
/// Woodland (Forage 100, Hunt 100) regrowing 50% a Round. Tiles are passed in as the Player sees them, so no map rows are seeded.
/// </para>
/// </remarks>
[TestFixture]
internal class TileSupportEvaluatorTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _GRASSLAND_TILE_ID = 101;
    private const int _OTHER_GRASSLAND_TILE_ID = 102;
    private const int _OCEAN_TILE_ID = 103;
    private const int _WOODLAND_TILE_ID = 104;

    private TileSupportEvaluator _evaluator = null!;

    [SetUp]
    public async Task SetUpEvaluatorAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.CampaignBiomes.AddRange(
                new CampaignBiome
                {
                    BiomeTypeId = BiomeType.Grassland,
                    Name = "Grassland",
                    MovementCost = 1m,
                    IsWater = false,
                    IsTraversable = true,
                    MapEditorHtmlColor = "#7cb342",
                    BaseForageValue = ScaledAmount.FromWhole(400).RawValue,
                    BaseHuntValue = ScaledAmount.FromWhole(200).RawValue,
                    BaseFishValue = null,
                    BaseFodderValue = ScaledAmount.FromWhole(1_000).RawValue
                },
                new CampaignBiome
                {
                    BiomeTypeId = BiomeType.Ocean,
                    Name = "Ocean",
                    MovementCost = 1m,
                    IsWater = true,
                    IsTraversable = true,
                    MapEditorHtmlColor = "#1565c0",
                    BaseForageValue = null,
                    BaseHuntValue = null,
                    BaseFishValue = ScaledAmount.FromWhole(300).RawValue,
                    BaseFodderValue = null
                },
                new CampaignBiome
                {
                    BiomeTypeId = BiomeType.Woodland,
                    Name = "Woodland",
                    MovementCost = 1m,
                    IsWater = false,
                    IsTraversable = true,
                    MapEditorHtmlColor = "#558b2f",
                    RegenerationPercent = 0.5m,
                    BaseForageValue = ScaledAmount.FromWhole(100).RawValue,
                    BaseHuntValue = ScaledAmount.FromWhole(100).RawValue,
                    BaseFishValue = null,
                    BaseFodderValue = null
                });

            await context.SaveChangesAsync();
        }

        _evaluator = new TileSupportEvaluator(new PlayerSustenanceValueReader(), new BiomeRegenerationValueReader());
    }

    [Test, Description("The Player's own Forces on a tile are summed into one need: 250 + 100 troops against 600 food is one Turn, where either alone would be two.")]
    public async Task ShouldSumThePlayersForcesOnTheTileAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 250);
        await AddForceAsync(unitInstanceId: 2, _GRASSLAND_TILE_ID, troops: 100);

        TileSupportLevel? level = await EvaluateSingleAsync(Grassland(_GRASSLAND_TILE_ID));

        Assert.That(level, Is.EqualTo(TileSupportLevel.OneTurn));
    }

    [Test, Description("Injured troops eat as well as healthy ones, exactly as the end-of-Turn feed counts them.")]
    public async Task ShouldCountInjuredTroopsAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 200, injured: 200);

        TileSupportLevel? level = await EvaluateSingleAsync(Grassland(_GRASSLAND_TILE_ID));

        Assert.That(level, Is.EqualTo(TileSupportLevel.OneTurn));
    }

    [Test, Description("Forces that do not feed - wiped out, in a Battle, or another Player's - add nothing to a tile's need, matching Gathering.")]
    public async Task ShouldIgnoreForcesThatDoNotFeedAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 0);
        await AddForceAsync(unitInstanceId: 2, _GRASSLAND_TILE_ID, troops: 1_000, battleMapTileId: 7);
        await AddForceAsync(unitInstanceId: 3, _GRASSLAND_TILE_ID, troops: 1_000, playerId: _OTHER_PLAYER_ID);

        TileSupportLevel? level = await EvaluateSingleAsync(Grassland(_GRASSLAND_TILE_ID));

        Assert.That(level, Is.Null, "With nobody of the Player's to feed, a tile with food has no level.");
    }

    [Test, Description("Requested Forces are counted on every tile evaluated, on top of the Forces already standing there - what the hover marker needs for a selection.")]
    public async Task ShouldAddTheRequestedForcesToEveryTileAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 200);
        await AddForceAsync(unitInstanceId: 2, _OTHER_GRASSLAND_TILE_ID, troops: 300);

        IReadOnlyDictionary<int, TileSupport?> levels = await EvaluateAsync([Grassland(_GRASSLAND_TILE_ID), Grassland(_OTHER_GRASSLAND_TILE_ID)], forceIds: [1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(levels[_GRASSLAND_TILE_ID]?.Level, Is.EqualTo(TileSupportLevel.Plenty), "The requested Force is already here, so the need is its 200 alone.");
            Assert.That(levels[_OTHER_GRASSLAND_TILE_ID]?.Level, Is.EqualTo(TileSupportLevel.OneTurn), "200 requested plus the 300 already there is 500 against 600.");
        }
    }

    [Test, Description("A requested Force standing on the tile being evaluated is counted once, not once as requested and again as present.")]
    public async Task ShouldNotCountARequestedForceOnTheTileTwiceAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 300);

        TileSupportLevel? level = await EvaluateSingleAsync(Grassland(_GRASSLAND_TILE_ID), forceIds: [1]);

        Assert.That(level, Is.EqualTo(TileSupportLevel.Plenty), "Counted twice, 600 against 600 would have been one Turn.");
    }

    [Test, Description("A requested Force of the Player's that is in a Battle adds nothing, rather than failing, since a selected Force may be fighting.")]
    public async Task ShouldIgnoreARequestedForceInABattleAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _OTHER_GRASSLAND_TILE_ID, troops: 1_000, battleMapTileId: 7);

        ServiceResult<IReadOnlyDictionary<int, TileSupport?>> result = await EvaluateResultAsync([Grassland(_GRASSLAND_TILE_ID)], [1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue![_GRASSLAND_TILE_ID], Is.Null);
        }
    }

    [TestCase(3, Description = "Another Player's Force is refused, so the client cannot learn the size of Forces that are not its own.")]
    [TestCase(99, Description = "An id that is no Force at all is refused rather than ignored.")]
    public async Task ShouldFailForARequestedForceThePlayerDoesNotOwnAsync(int forceId)
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 100);
        await AddForceAsync(unitInstanceId: 3, _OTHER_GRASSLAND_TILE_ID, troops: 100, playerId: _OTHER_PLAYER_ID);

        ServiceResult<IReadOnlyDictionary<int, TileSupport?>> result = await EvaluateResultAsync([Grassland(_GRASSLAND_TILE_ID)], [1, forceId]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
        }
    }

    [Test, Description("A stripped tile is exhausted even with nobody on it, so the toolbar shows a bare tile as bare.")]
    public async Task ShouldBeExhaustedOnAStrippedTileWithNoForcesAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 400, huntTaken: 200);

        TileSupportLevel? level = await EvaluateSingleAsync(Grassland(_GRASSLAND_TILE_ID));

        Assert.That(level, Is.EqualTo(TileSupportLevel.Exhausted));
    }

    [Test, Description("What has been taken is subtracted before classifying: 300 left against 200 troops is one Turn, where the untouched 600 would be plenty.")]
    public async Task ShouldClassifyAgainstWhatIsLeftAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 200, huntTaken: 100);
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 200);

        TileSupportLevel? level = await EvaluateSingleAsync(Grassland(_GRASSLAND_TILE_ID));

        Assert.That(level, Is.EqualTo(TileSupportLevel.OneTurn));
    }

    [Test, Description("A water tile's pool is Fish alone: 300 Fish against 200 troops is one Turn.")]
    public async Task ShouldUseOnlyFishOnAWaterTileAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _OCEAN_TILE_ID, troops: 200);

        var ocean = BuildTile(_OCEAN_TILE_ID, BiomeType.Ocean, CampaignMapGeographyType.DeepWater, biomeIsWater: true);

        TileSupportLevel? level = await EvaluateSingleAsync(ocean);

        Assert.That(level, Is.EqualTo(TileSupportLevel.OneTurn));
    }

    [Test, Description("A tile seen by several Observers is passed in more than once, and is evaluated once without failing.")]
    public async Task ShouldEvaluateADuplicatedTileOnceAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 100);

        IReadOnlyDictionary<int, TileSupport?> levels = await EvaluateAsync([Grassland(_GRASSLAND_TILE_ID), Grassland(_GRASSLAND_TILE_ID)], forceIds: []);

        Assert.That(levels, Has.Count.EqualTo(1));
    }

    [Test, Description("The biome's regrowth is counted: 150 troops on 200 Woodland food is one Turn by plain division, but 100 regrown a Round makes it two.")]
    public async Task ShouldCountTheTurnsTheBiomesRegrowthAddsAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _WOODLAND_TILE_ID, troops: 150);

        IReadOnlyDictionary<int, TileSupport?> supports = await EvaluateAsync([BuildTile(_WOODLAND_TILE_ID, BiomeType.Woodland, CampaignMapGeographyType.Flat, biomeIsWater: false)], forceIds: []);

        Assert.That(supports[_WOODLAND_TILE_ID], Is.EqualTo(new TileSupport(TileSupportLevel.Plenty, 2)), "200, then 150 after regrowth, then 100: two Turns.");
    }

    [Test, Description("A biome with no Regeneration Percent does not regrow, so its Turns are plain division: 600 Grassland food feeds 250 troops for two Turns.")]
    public async Task ShouldCountPlainTurnsWithoutARegenerationPercentAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 250);

        IReadOnlyDictionary<int, TileSupport?> supports = await EvaluateAsync([Grassland(_GRASSLAND_TILE_ID)], forceIds: []);

        Assert.That(supports[_GRASSLAND_TILE_ID], Is.EqualTo(new TileSupport(TileSupportLevel.Plenty, 2)));
    }

    private async Task<ServiceResult<IReadOnlyDictionary<int, TileSupport?>>> EvaluateResultAsync(SightTerrain[] tiles, int[] forceIds)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _evaluator.EvaluateAsync(context, _GAME_ID, _PLAYER_ID, tiles, forceIds);
    }

    private async Task<TileSupportLevel?> EvaluateSingleAsync(SightTerrain tile, int[]? forceIds = null)
    {
        IReadOnlyDictionary<int, TileSupport?> supports = await EvaluateAsync([tile], forceIds ?? []);

        return supports[tile.CampaignMapId]?.Level;
    }

    private async Task<IReadOnlyDictionary<int, TileSupport?>> EvaluateAsync(SightTerrain[] tiles, int[] forceIds)
    {
        ServiceResult<IReadOnlyDictionary<int, TileSupport?>> result = await EvaluateResultAsync(tiles, forceIds);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    /// <summary>Adds a Force, defaulting to the acting Player's own, on the campaign map.</summary>
    private async Task AddForceAsync(int unitInstanceId, int tileId, int troops, int injured = 0, int playerId = _PLAYER_ID, int? battleMapTileId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapTileId = tileId,
            BattleMapTileId = battleMapTileId,
            UnitTemplateId = 1,
            InitialStrength = Math.Max(troops + injured, 1),
            HealthyCount = troops,
            InjuredCount = injured
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Records whole amounts as already taken from a tile.</summary>
    private async Task AddDepletionAsync(int tileId, long forageTaken, long huntTaken)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            ForageTaken = ScaledAmount.FromWhole(forageTaken).RawValue,
            HuntTaken = ScaledAmount.FromWhole(huntTaken).RawValue,
            FedOnRound = 1
        });

        await context.SaveChangesAsync();
    }

    private static SightTerrain Grassland(int tileId) => BuildTile(tileId, BiomeType.Grassland, CampaignMapGeographyType.Flat, biomeIsWater: false);

    private static SightTerrain BuildTile(int tileId, BiomeType biome, CampaignMapGeographyType geography, bool biomeIsWater)
    {
        return new SightTerrain
        {
            CampaignMapId = tileId,
            X = tileId % 10,
            Y = 0,
            BiomeType = biome,
            GeographyType = geography,
            DoesBlockSight = false,
            RelativeElevation = 0,
            GeographyName = geography.ToString(),
            GeographyMovementCost = 1m,
            BiomeName = biome.ToString(),
            BiomeMovementCost = 1m,
            BiomeIsWater = biomeIsWater,
            BiomeIsTraversable = true
        };
    }
}
