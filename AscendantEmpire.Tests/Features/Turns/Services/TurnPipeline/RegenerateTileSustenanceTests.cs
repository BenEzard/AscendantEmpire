using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="RegenerateTileSustenance"/>: which tiles and resources regrow at the end of a Round, and how being fed on this Round is read.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The process reads and writes through LINQ and the change tracker only.
/// </para>
/// <para>
/// The regrowth rules and their rounding are pinned separately, without a database, in <c>TileRegenerationCalculatorTests</c>. What is tested here is the orchestration
/// around them.
/// </para>
/// <para>
/// The catalogue has Grassland (Forage 400, Hunt 200, no Fish, Fodder 1,000, 10%), Ocean (Fish 300 only, 25%) and a legacy Desert (Forage 100, no Regeneration Percent). There are
/// two Grassland tiles, an Ocean tile and a Desert tile in this Game, and a Grassland tile in another Game.
/// </para>
/// </remarks>
[TestFixture]
internal class RegenerateTileSustenanceTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;
    private const int _GRASSLAND_TILE_ID = 101;
    private const int _OTHER_GRASSLAND_TILE_ID = 102;
    private const int _OCEAN_TILE_ID = 103;
    private const int _DESERT_TILE_ID = 104;
    private const int _OTHER_GAME_TILE_ID = 201;
    private const int _CURRENT_ROUND_NUMBER = 4;

    private RegenerateTileSustenance _process = null!;

    [SetUp]
    public async Task SetUpProcessAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            var flat = new CampaignGeography { CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat, Name = "Flat", MovementCost = 1m, MapEditorIcon = null, IsPaintableOnCampaignMap = true };

            var grassland = new CampaignBiome
            {
                BiomeTypeId = BiomeType.Grassland,
                Name = "Grassland",
                MovementCost = 1m,
                IsWater = false,
                IsTraversable = true,
                MapEditorHtmlColor = "#7cb342",
                RegenerationPercent = 0.1m,
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
                RegenerationPercent = 0.25m,
                BaseForageValue = null,
                BaseHuntValue = null,
                BaseFishValue = ScaledAmount.FromWhole(300).RawValue,
                BaseFodderValue = null
            };

            // Saved before the biome validator required a Regeneration Percent wherever there is a sustenance base.
            var legacyDesert = new CampaignBiome
            {
                BiomeTypeId = BiomeType.Desert,
                Name = "Desert",
                MovementCost = 1m,
                IsWater = false,
                IsTraversable = true,
                MapEditorHtmlColor = "#e0c068",
                RegenerationPercent = null,
                BaseForageValue = ScaledAmount.FromWhole(100).RawValue
            };

            context.CampaignGeographies.Add(flat);
            context.CampaignBiomes.AddRange(grassland, ocean, legacyDesert);

            context.CampaignMaps.AddRange(
                BuildTile(_GRASSLAND_TILE_ID, _GAME_ID, x: 0, grassland, flat),
                BuildTile(_OTHER_GRASSLAND_TILE_ID, _GAME_ID, x: 1, grassland, flat),
                BuildTile(_OCEAN_TILE_ID, _GAME_ID, x: 2, ocean, flat),
                BuildTile(_DESERT_TILE_ID, _GAME_ID, x: 3, legacyDesert, flat),
                BuildTile(_OTHER_GAME_TILE_ID, _OTHER_GAME_ID, x: 0, grassland, flat));

            await context.SaveChangesAsync();
        }

        _process = new RegenerateTileSustenance(DbFactory, new BiomeRegenerationValueReader(), NullLogger<RegenerateTileSustenance>.Instance);
    }

    [Test, Description("A tile fed on this Round regrows each resource by its own Base: partly depleted Forage and bare Hunt both regrow under the Force.")]
    public async Task ShouldRegrowEachResourceIndependentlyOnATileFedOnThisRoundAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 100_000, huntTaken: 200_000, fedOnRound: _CURRENT_ROUND_NUMBER);

        string error = await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);

        // Forage regrows by 10% of its Base of 400, Hunt by 10% of its Base of 200.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(depletion.ForageTaken, Is.EqualTo(60_000));
            Assert.That(depletion.HuntTaken, Is.EqualTo(180_000), "A bare resource regrows even while it is fed on.");
            Assert.That(depletion.FodderTaken, Is.Zero);
        }
    }

    [TestCase(_CURRENT_ROUND_NUMBER - 1, Description = "A tile last fed on in an earlier Round was left alone this Round.")]
    [TestCase(null, Description = "A tile with no fed-on marker was left alone this Round.")]
    public async Task ShouldTreatATileNotFedOnThisRoundAsLeftAloneAsync(int? fedOnRound)
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 400_000, huntTaken: 50_000, fedOnRound: fedOnRound);

        await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.ForageTaken, Is.EqualTo(360_000), "Bare Forage left alone recovers to 10% of Base.");
            Assert.That(depletion.HuntTaken, Is.EqualTo(30_000), "Hunt regrows by 10% of its Base of 200.");
        }
    }

    [Test, Description("Being fed on only counts for the Round it happened in, so the next Round's end treats the tile as left alone with nothing needing clearing.")]
    public async Task ShouldTreatTheFedOnMarkerAsClearedInTheNextRoundAsync()
    {
        // Hunt above its Base of 200, as a stronger Player would leave it, so the 10% floor gives a different answer from regrowth.
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 0, huntTaken: 250_000, fedOnRound: _CURRENT_ROUND_NUMBER);

        await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));
        long huntTakenAfterFedOnRound = (await ReloadDepletionAsync(_GRASSLAND_TILE_ID)).HuntTaken;

        await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER + 1));
        long huntTakenAfterRoundLeftAlone = (await ReloadDepletionAsync(_GRASSLAND_TILE_ID)).HuntTaken;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(huntTakenAfterFedOnRound, Is.EqualTo(230_000), "Fed on: regrowth only.");
            Assert.That(huntTakenAfterRoundLeftAlone, Is.EqualTo(180_000), "Left alone: the 10% floor beats regrowth to 210.");
        }
    }

    [Test, Description("A resource the biome does not have is never regenerated, even if its Taken column holds something.")]
    public async Task ShouldLeaveAResourceWithNoBaseUntouchedAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 100_000, huntTaken: 0, fedOnRound: null, fishTaken: 5_000);

        await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);

        Assert.That(depletion.FishTaken, Is.EqualTo(5_000));
    }

    [Test, Description("Every depleted tile in the Game regrows in the one run, each at its own biome's Regeneration Percent.")]
    public async Task ShouldRegrowEveryDepletedTileInTheGameAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 100_000, huntTaken: 0, fedOnRound: _CURRENT_ROUND_NUMBER);
        await AddDepletionAsync(_OTHER_GRASSLAND_TILE_ID, forageTaken: 200_000, huntTaken: 0, fedOnRound: _CURRENT_ROUND_NUMBER);
        await AddDepletionAsync(_OCEAN_TILE_ID, forageTaken: 0, huntTaken: 0, fedOnRound: _CURRENT_ROUND_NUMBER, fishTaken: 150_000);

        await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await ReloadDepletionAsync(_GRASSLAND_TILE_ID)).ForageTaken, Is.EqualTo(60_000));
            Assert.That((await ReloadDepletionAsync(_OTHER_GRASSLAND_TILE_ID)).ForageTaken, Is.EqualTo(160_000));
            Assert.That((await ReloadDepletionAsync(_OCEAN_TILE_ID)).FishTaken, Is.EqualTo(75_000), "25% of a Fish Base of 300 regrows 75.");
        }
    }

    [Test, Description("Only the ending Game's tiles regrow, so another Game's depletion is left for its own Round to handle.")]
    public async Task ShouldLeaveAnotherGamesTilesUntouchedAsync()
    {
        await AddDepletionAsync(_OTHER_GAME_TILE_ID, forageTaken: 100_000, huntTaken: 0, fedOnRound: null, gameId: _OTHER_GAME_ID);

        await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_OTHER_GAME_TILE_ID);

        Assert.That(depletion.ForageTaken, Is.EqualTo(100_000));
    }

    [Test, Description("A tile that regrows fully keeps its depletion row, ready to be reused the next time a Force feeds there.")]
    public async Task ShouldKeepARowThatRegrowsToZeroAsync()
    {
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 10_000, huntTaken: 0, fedOnRound: _CURRENT_ROUND_NUMBER);

        await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_GRASSLAND_TILE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.ForageTaken, Is.Zero);
            Assert.That(depletion.FedOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER), "Regeneration never writes the fed-on marker.");
        }
    }

    [Test, Description("A legacy biome with a sustenance base but no Regeneration Percent leaves its tile as it is, without stopping other tiles regrowing or the Round ending.")]
    public async Task ShouldSkipATileWhoseBiomeHasNoRegenerationPercentAsync()
    {
        await AddDepletionAsync(_DESERT_TILE_ID, forageTaken: 100_000, huntTaken: 0, fedOnRound: null);
        await AddDepletionAsync(_GRASSLAND_TILE_ID, forageTaken: 100_000, huntTaken: 0, fedOnRound: _CURRENT_ROUND_NUMBER);

        string error = await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That((await ReloadDepletionAsync(_DESERT_TILE_ID)).ForageTaken, Is.EqualTo(100_000));
            Assert.That((await ReloadDepletionAsync(_GRASSLAND_TILE_ID)).ForageTaken, Is.EqualTo(60_000));
        }
    }

    [Test, Description("A Game with nothing depleted ends its Round without error and without creating any depletion rows.")]
    public async Task ShouldSucceedWhenNothingIsDepletedAsync()
    {
        string error = await _process.ProcessAsync(EndRoundContext(_CURRENT_ROUND_NUMBER));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(await context.CampaignMapDepletions.AnyAsync(), Is.False);
        }
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    private static TurnPipelineContextData EndRoundContext(int roundNumber) =>
        new(TurnPipelineType.EndCampaignRound, _GAME_ID, roundNumber);

    private static CampaignMap BuildTile(int tileId, int gameId, int x, CampaignBiome biome, CampaignGeography geography)
    {
        return new CampaignMap
        {
            CampaignMapId = tileId,
            GameId = gameId,
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

    private async Task AddDepletionAsync(int tileId, long forageTaken, long huntTaken, int? fedOnRound, long fishTaken = 0, int gameId = _GAME_ID)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = gameId,
            ForageTaken = forageTaken,
            HuntTaken = huntTaken,
            FishTaken = fishTaken,
            FedOnRound = fedOnRound
        });

        await context.SaveChangesAsync();
    }

    private async Task<CampaignMapDepletion> ReloadDepletionAsync(int tileId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMapDepletions.SingleAsync(d => d.CampaignMapId == tileId);
    }
}
