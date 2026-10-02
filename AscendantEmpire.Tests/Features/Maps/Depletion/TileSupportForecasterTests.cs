using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="TileSupportForecaster"/>: how many Turns a tile feeds a need with regrowth at the end of each Round, and the support level that earns.
/// </summary>
/// <remarks>
/// <para>
/// No database - the forecaster is pure. Which Forces make up the need, and reading the values, are covered in <c>TileSupportEvaluatorTests</c>.
/// </para>
/// <para>
/// Grassland has Forage 400 and Hunt 200: 600 food on land. At 25% it regrows 100 Forage and 50 Hunt a Round, 150 in all. Ocean has Fish 300 only. Both resources regrow in
/// the same 2:1 ratio they are eaten in, so the pool's total simply falls by <c>need − 150</c> a Round and the expected Turns can be read off by hand.
/// </para>
/// </remarks>
[TestFixture]
internal class TileSupportForecasterTests
{
    private static readonly PlayerSustenanceValues _VALUES = new(playerId: 1, new Dictionary<(BiomeType Biome, SustenanceResource Resource), ScaledAmount?>
    {
        [(BiomeType.Grassland, SustenanceResource.Forage)] = ScaledAmount.FromWhole(400),
        [(BiomeType.Grassland, SustenanceResource.Hunt)] = ScaledAmount.FromWhole(200),
        [(BiomeType.Grassland, SustenanceResource.Fish)] = null,
        [(BiomeType.Grassland, SustenanceResource.Fodder)] = null,
        [(BiomeType.Ocean, SustenanceResource.Forage)] = null,
        [(BiomeType.Ocean, SustenanceResource.Hunt)] = null,
        [(BiomeType.Ocean, SustenanceResource.Fish)] = ScaledAmount.FromWhole(300),
        [(BiomeType.Ocean, SustenanceResource.Fodder)] = null
    });

    [TestCase(200, 3, TileSupportLevel.Plenty, Description = "600 food feeds 200 for three whole Turns.")]
    [TestCase(300, 2, TileSupportLevel.Plenty, Description = "Exactly two Turns of food is plenty: the boundary is inclusive.")]
    [TestCase(300.001, 1, TileSupportLevel.OneTurn, Description = "A thousandth over half the pool is only one Turn.")]
    [TestCase(600, 1, TileSupportLevel.OneTurn, Description = "Exactly one Turn of food is one Turn: the boundary is inclusive.")]
    [TestCase(600.001, 0, TileSupportLevel.Insufficient, Description = "A thousandth short of one Turn cannot feed the whole Force.")]
    public void ShouldMatchPlainDivisionWhenTheBiomeHasNoRegenerationPercent(decimal need, int expectedTurns, TileSupportLevel expectedLevel)
    {
        TileSupport? support = ForecastGrassland(ScaledAmount.FromDecimal(need), regenerationPercent: null);

        Assert.That(support, Is.EqualTo(new TileSupport(expectedLevel, expectedTurns)));
    }

    [Test, Description("With no regeneration values found at all, the tile does not regrow and the forecast is plain division rather than a failure.")]
    public void ShouldNotRegrowWithoutRegenerationValues()
    {
        TileSupport? support = TileSupportForecaster.Forecast(ScaledAmount.FromWhole(250), isWater: false, _VALUES, BiomeType.Grassland, depletion: null, regeneration: null);

        Assert.That(support, Is.EqualTo(new TileSupport(TileSupportLevel.Plenty, 2)));
    }

    [Test, Description("Regrowth adds Turns: 250 a Turn against 150 regrown a Round leaves 600, 500, 400, 300 - four Turns where plain division gives two.")]
    public void ShouldCountTheTurnsRegrowthAdds()
    {
        TileSupport? support = ForecastGrassland(ScaledAmount.FromWhole(250), regenerationPercent: 0.25m);

        Assert.That(support, Is.EqualTo(new TileSupport(TileSupportLevel.Plenty, 4)));
    }

    [Test, Description("A need no larger than the regrowth is fed forever, so the tile lasts beyond the forecast and reports no count of Turns.")]
    public void ShouldLastBeyondTheForecastWhenRegrowthCoversTheNeed()
    {
        TileSupport? support = ForecastGrassland(ScaledAmount.FromWhole(150), regenerationPercent: 0.25m);

        Assert.That(support, Is.EqualTo(new TileSupport(TileSupportLevel.Plenty, null)));
    }

    [TestCase(230, 5, Description = "230 a Turn leaves 600, 520, 440, 360, 280 then 200: the sixth Turn goes short, so exactly the last Turn shown.")]
    [TestCase(220, null, Description = "220 a Turn leaves 250 for a sixth Turn, so it lasts beyond the forecast.")]
    public void ShouldStopTheForecastAfterTheLastTurnShown(int need, int? expectedTurns)
    {
        TileSupport? support = ForecastGrassland(ScaledAmount.FromWhole(need), regenerationPercent: 0.25m);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TileSupportForecaster.MAX_FORECAST_TURNS, Is.EqualTo(5), "The cases above are worked out for a forecast of five Turns.");
            Assert.That(support, Is.EqualTo(new TileSupport(TileSupportLevel.Plenty, expectedTurns)));
        }
    }

    [Test, Description("Forage taken above the Player's value is paid off by regrowth before any shows, so only the Hunt feeds: one Turn, where a tile at the Player's value would give more.")]
    public void ShouldPayOffTakenAboveThePlayersValueBeforeFoodShows()
    {
        // A stronger Player ate 500 Forage, 100 past this Player's 400. Regrowing 100 a Round only brings it back to 400: still nothing this Player can reach.
        var depletion = new CampaignMapDepletion { ForageTaken = ScaledAmount.FromWhole(500).RawValue };

        TileSupport? support = TileSupportForecaster.Forecast(
            ScaledAmount.FromWhole(150),
            isWater: false,
            _VALUES,
            BiomeType.Grassland,
            depletion,
            GrasslandRegeneration(0.25m));

        Assert.That(support, Is.EqualTo(new TileSupport(TileSupportLevel.OneTurn, 1)));
    }

    [Test, Description("A water tile feeds from Fish alone and regrows it: 120 a Turn against 30 a Round leaves 300, 210, 120 - three Turns where plain division gives two.")]
    public void ShouldForecastAWaterTileFromFish()
    {
        var oceanRegeneration = new BiomeRegenerationValues
        {
            BiomeType = BiomeType.Ocean,
            RegenerationPercent = 0.1m,
            BaseFishValue = ScaledAmount.FromWhole(300).RawValue
        };

        TileSupport? support = TileSupportForecaster.Forecast(ScaledAmount.FromWhole(120), isWater: true, _VALUES, BiomeType.Ocean, depletion: null, oceanRegeneration);

        Assert.That(support, Is.EqualTo(new TileSupport(TileSupportLevel.Plenty, 3)));
    }

    [TestCase(100, Description = "A tile with nothing left is exhausted for Forces standing on it, even though it will regrow: they starve this Turn.")]
    [TestCase(0, Description = "A tile with nothing left is exhausted even with nobody on it, so a stripped tile always reads as stripped.")]
    public void ShouldBeExhaustedWhenNothingIsAvailable(int need)
    {
        var depletion = new CampaignMapDepletion { ForageTaken = ScaledAmount.FromWhole(400).RawValue, HuntTaken = ScaledAmount.FromWhole(200).RawValue };

        TileSupport? support = TileSupportForecaster.Forecast(ScaledAmount.FromWhole(need), isWater: false, _VALUES, BiomeType.Grassland, depletion, GrasslandRegeneration(0.25m));

        Assert.That(support, Is.EqualTo(new TileSupport(TileSupportLevel.Exhausted, 0)));
    }

    [Test, Description("A tile with food but nobody to feed has no support, so nothing is coloured.")]
    public void ShouldReturnNullWhenThereIsNoNeedAndFoodIsAvailable()
    {
        TileSupport? support = ForecastGrassland(ScaledAmount.Zero, regenerationPercent: 0.25m);

        Assert.That(support, Is.Null);
    }

    [Test, Description("The forecast plays out on a copy, so the caller's depletion row - possibly tracked - is left exactly as it was.")]
    public void ShouldNotChangeTheCallersDepletionRow()
    {
        var depletion = new CampaignMapDepletion { CampaignMapId = 7, GameId = 1, ForageTaken = 1_000, HuntTaken = 2_000, FedOnRound = 3 };

        TileSupportForecaster.Forecast(ScaledAmount.FromWhole(250), isWater: false, _VALUES, BiomeType.Grassland, depletion, GrasslandRegeneration(0.25m));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.ForageTaken, Is.EqualTo(1_000));
            Assert.That(depletion.HuntTaken, Is.EqualTo(2_000));
            Assert.That(depletion.FishTaken, Is.Zero);
            Assert.That(depletion.FodderTaken, Is.Zero);
            Assert.That(depletion.FedOnRound, Is.EqualTo(3));
        }
    }

    [Test, Description("A negative need is a defect in the caller and is refused rather than forecast.")]
    public void ShouldThrowForANegativeNeed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ForecastGrassland(ScaledAmount.FromWhole(-1), regenerationPercent: 0.25m));
    }

    /// <summary>Forecasts an untouched Grassland tile at the given Regeneration Percent.</summary>
    private static TileSupport? ForecastGrassland(ScaledAmount need, decimal? regenerationPercent)
    {
        return TileSupportForecaster.Forecast(need, isWater: false, _VALUES, BiomeType.Grassland, depletion: null, GrasslandRegeneration(regenerationPercent));
    }

    /// <summary>Grassland's regeneration values: the same bases as the Player's values, at the given Percent.</summary>
    private static BiomeRegenerationValues GrasslandRegeneration(decimal? regenerationPercent)
    {
        return new BiomeRegenerationValues
        {
            BiomeType = BiomeType.Grassland,
            RegenerationPercent = regenerationPercent,
            BaseForageValue = ScaledAmount.FromWhole(400).RawValue,
            BaseHuntValue = ScaledAmount.FromWhole(200).RawValue
        };
    }
}
