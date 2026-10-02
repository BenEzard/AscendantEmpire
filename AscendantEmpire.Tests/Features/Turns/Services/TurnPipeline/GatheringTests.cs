using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="Gathering"/>: which Forces feed at the end of a Turn, what they take from their tile, and what is handed on about the ones that went short.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The process reads and writes through LINQ and the change tracker only, and projects the Forces to scalars rather than materialising them. The
/// Player's values come from the real <see cref="PlayerSustenanceValueReader"/>, so a modifier here is applied exactly as in play; the underfed resolver is substituted, so
/// what it is given can be inspected.
/// </para>
/// <para>
/// The split arithmetic is pinned separately, without a database, in <c>FoodExtractionCalculatorTests</c> and <c>ShortfallAllocatorTests</c>. What is tested here is the
/// orchestration around it.
/// </para>
/// <para>
/// The catalogue has Grassland (Forage 400, Hunt 200, no Fish, Fodder 1,000) and Ocean (Fish 300 only). There are two Grassland tiles and one Ocean tile.
/// </para>
/// </remarks>
[TestFixture]
internal class GatheringTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _GRASSLAND_TILE_ID = 101;
    private const int _OTHER_GRASSLAND_TILE_ID = 102;
    private const int _OCEAN_TILE_ID = 103;
    private const int _CURRENT_ROUND_NUMBER = 4;
    private const int _PLAYER_TURN_ID = 77;

    private IUnderfedForceResolver _underfedForceResolver = null!;
    private Gathering _process = null!;

    [SetUp]
    public async Task SetUpProcessAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            var flat = new CampaignGeography { CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat, Name = "Flat", MovementCost = 1m, MapEditorIcon = null, IsPaintableOnCampaignMap = true };
            var deepWater = new CampaignGeography { CampaignMapGeographyTypeId = CampaignMapGeographyType.DeepWater, Name = "Deep Water", MovementCost = 1m, MapEditorIcon = null, IsPaintableOnCampaignMap = true };

            var grassland = new CampaignBiome
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
            };

            var ocean = new CampaignBiome
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
            };

            context.CampaignGeographies.AddRange(flat, deepWater);
            context.CampaignBiomes.AddRange(grassland, ocean);

            context.CampaignMaps.AddRange(
                BuildTile(_GRASSLAND_TILE_ID, x: 0, grassland, flat),
                BuildTile(_OTHER_GRASSLAND_TILE_ID, x: 1, grassland, flat),
                BuildTile(_OCEAN_TILE_ID, x: 2, ocean, deepWater));

            await context.SaveChangesAsync();
        }

        _underfedForceResolver = Substitute.For<IUnderfedForceResolver>();

        _process = new Gathering(DbFactory, new PlayerSustenanceValueReader(), _underfedForceResolver, NullLogger<Gathering>.Instance);
    }

    [Test, Description("The first feed on a tile creates its depletion row, splitting the need across Forage and Hunt in proportion to what each has.")]
    public async Task ShouldCreateTheDepletionRowAndAddToTakenOnTheFirstFeedAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 100);

        string error = await _process.ProcessAsync(EndTurnContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(depletion.GameId, Is.EqualTo(_GAME_ID));
            Assert.That(depletion.ForageTaken, Is.EqualTo(66_667));
            Assert.That(depletion.HuntTaken, Is.EqualTo(33_333));
            Assert.That(depletion.FishTaken, Is.Zero);
            Assert.That(depletion.FodderTaken, Is.Zero, "Fodder is for animals, and men do not eat it.");
        }
    }

    [Test, Description("A tile already fed on adds to what was taken before, since Taken persists between Turns and is shared by everyone.")]
    public async Task ShouldAddToTheExistingTakenOnALaterFeedAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 100_000, huntTaken: 50_000, fedOnRound: 1);
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 90);

        await _process.ProcessAsync(EndTurnContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);

        // 300 Forage and 150 Hunt were left, so 90 splits 60 / 30.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.ForageTaken, Is.EqualTo(160_000));
            Assert.That(depletion.HuntTaken, Is.EqualTo(80_000));
        }
    }

    [Test, Description("A Player whose modifier raises their Hunt value reaches food a base Player cannot, and goes without less on a stripped tile.")]
    public async Task ShouldLetAPlayerWithAModifierReachMoreFoodAsync()
    {
        // Stripped at base values: 400 Forage and 200 Hunt already taken.
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 400_000, huntTaken: 200_000, fedOnRound: 1);
        await AddHuntModifierAsync(flatWhole: 50);
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 100);

        await _process.ProcessAsync(EndTurnContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);
        ForceFeedingResult result = SingleResolvedResult();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.HuntTaken, Is.EqualTo(250_000), "The Player's surplus of 50 Hunt was theirs to take.");
            Assert.That(result.Fed, Is.EqualTo(ScaledAmount.FromWhole(50)));
            Assert.That(result.Shortfall, Is.EqualTo(ScaledAmount.FromWhole(50)));
        }
    }

    [Test, Description("A water tile's pool is Fish alone, so a Force at sea never takes Forage or Hunt.")]
    public async Task ShouldTakeOnlyFishOnAWaterTileAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _OCEAN_TILE_ID, troops: 100);

        await _process.ProcessAsync(EndTurnContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_OCEAN_TILE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.FishTaken, Is.EqualTo(100_000));
            Assert.That(depletion.ForageTaken, Is.Zero);
            Assert.That(depletion.HuntTaken, Is.Zero);
        }
    }

    [Test, Description("A tile is marked fed on this Round even when it had nothing left to take, because the marker records presence and a bare tile must not refill under a Force.")]
    public async Task ShouldMarkTheTileFedOnEvenWhenNothingIsAvailableAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 400_000, huntTaken: 200_000, fedOnRound: 1);
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 100);

        await _process.ProcessAsync(EndTurnContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.FedOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER));
            Assert.That(depletion.ForageTaken, Is.EqualTo(400_000), "Nothing more could be taken.");
            Assert.That(depletion.HuntTaken, Is.EqualTo(200_000));
        }
    }

    [Test, Description("Forces in a Battle, wiped-out Forces and other Players' Forces do not feed, so none of them leaves a depletion row behind.")]
    public async Task ShouldIgnoreForcesInBattleWipedOutOrBelongingToAnotherPlayerAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 100, battleMapTileId: 900);
        await AddForceAsync(unitInstanceId: 2, _OTHER_GRASSLAND_TILE_ID, troops: 0);
        await AddForceAsync(unitInstanceId: 3, _OCEAN_TILE_ID, troops: 100, playerId: _OTHER_PLAYER_ID);

        string error = await _process.ProcessAsync(EndTurnContext());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(await context.CampaignMapDepletions.AnyAsync(), Is.False);
            await _underfedForceResolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default, default, default, default, default!, default);
        }
    }

    [Test, Description("Forces sharing a tile feed from one pool, and its shortfall is shared in proportion to their troops, so every Force on the tile goes equally short.")]
    public async Task ShouldHandTheResolverEachForcesShareOfTheShortfallAsync()
    {
        // 400 Forage and 200 Hunt available; 800 troops need 800, so the tile is 200 short.
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 600);
        await AddForceAsync(unitInstanceId: 2, _GRASSLAND_TILE_ID, troops: 200);

        await _process.ProcessAsync(EndTurnContext());

        IReadOnlyCollection<ForceFeedingResult> results = ResolvedResults();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.EquivalentTo(new[]
            {
                new ForceFeedingResult(1, _GRASSLAND_TILE_ID, ScaledAmount.FromWhole(600), ScaledAmount.FromWhole(450), ScaledAmount.FromWhole(150)),
                new ForceFeedingResult(2, _GRASSLAND_TILE_ID, ScaledAmount.FromWhole(200), ScaledAmount.FromWhole(150), ScaledAmount.FromWhole(50))
            }));

            await _underfedForceResolver.Received(1).ResolveAsync(
                Arg.Any<AscendantEmpireContext>(),
                _GAME_ID,
                _PLAYER_ID,
                _CURRENT_ROUND_NUMBER,
                _PLAYER_TURN_ID,
                Arg.Any<IReadOnlyCollection<ForceFeedingResult>>(),
                Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("An end-of-Turn context carrying no Player Id fails the pipeline rather than feeding every Player's Forces in the Game.")]
    public async Task ShouldFailWhenTheContextCarriesNoPlayerAsync()
    {
        await AddForceAsync(unitInstanceId: 1, _GRASSLAND_TILE_ID, troops: 100);

        string error = await _process.ProcessAsync(new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, _GAME_ID, _CURRENT_ROUND_NUMBER));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Not.Empty);
            Assert.That(await context.CampaignMapDepletions.AnyAsync(), Is.False, "Nothing was fed.");
        }
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    private static TurnPipelineContextData EndTurnContext() =>
        new(TurnPipelineType.EndCampaignTurn, _GAME_ID, _PLAYER_ID, _CURRENT_ROUND_NUMBER)
        {
            PlayerTurnId = _PLAYER_TURN_ID
        };

    private static CampaignMap BuildTile(int tileId, int x, CampaignBiome biome, CampaignGeography geography)
    {
        return new CampaignMap
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            XCoord = x,
            YCoord = 0,
            BiomeTypeId = biome.BiomeTypeId,
            CampaignMapGeographyTypeId = geography.CampaignMapGeographyTypeId,
            Biome = biome,
            Geography = geography,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            ControlledBy = null
        };
    }

    /// <summary>Adds a Force, defaulting to the acting Player's own, all healthy, on the campaign map.</summary>
    private async Task AddForceAsync(int unitInstanceId, int tileId, int troops, int playerId = _PLAYER_ID, int? battleMapTileId = null)
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
            InitialStrength = Math.Max(troops, 1),
            HealthyCount = troops,
            InjuredCount = 0
        });

        await context.SaveChangesAsync();
    }

    private async Task AddDepletionAsync(int tileId, long forageTaken, long huntTaken, int fedOnRound)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            ForageTaken = forageTaken,
            HuntTaken = huntTaken,
            FedOnRound = fedOnRound
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Gives the acting Player a flat Hunt modifier in every biome.</summary>
    private async Task AddHuntModifierAsync(long flatWhole)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerSustenanceModifiers.Add(new PlayerSustenanceModifier
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            SourceType = ModifierSourceType.Technology,
            SourceId = 10,
            Resource = SustenanceResource.Hunt,
            BiomeTypeId = null,
            FlatValue = ScaledAmount.FromWhole(flatWhole).RawValue,
            PercentValue = 0
        });

        await context.SaveChangesAsync();
    }

    private async Task<CampaignMapDepletion> ReloadDepletionAsync(int tileId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMapDepletions.SingleAsync(d => d.CampaignMapId == tileId);
    }

    /// <summary>The results the process handed the underfed resolver on its one call.</summary>
    private IReadOnlyCollection<ForceFeedingResult> ResolvedResults()
    {
        return (IReadOnlyCollection<ForceFeedingResult>)_underfedForceResolver.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IUnderfedForceResolver.ResolveAsync))
            .GetArguments()[5]!;
    }

    private ForceFeedingResult SingleResolvedResult()
    {
        return ResolvedResults().Single();
    }
}
