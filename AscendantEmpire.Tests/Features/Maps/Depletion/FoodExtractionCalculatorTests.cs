using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="FoodExtractionCalculator"/>: how a food need is split across a tile's sources, and what is left unmet.
/// </summary>
/// <remarks>
/// No database - the calculator is pure. Which sources a tile offers, and what is available of each, are <c>TileFoodSources</c>' to decide and are covered
/// in its own fixture. Unless a test says otherwise every source is priced at 1.0, so an Asset amount and a number of rations are the same number.
/// </remarks>
[TestFixture]
internal class FoodExtractionCalculatorTests
{
    #region Fixture

    private const int _FORAGE_ASSET = 10;
    private const int _GAME_ASSET = 11;
    private const int _BERRIES_ASSET = 20;

    /// <summary>
    /// What most foodstuffs cost to feed one individual for one Turn.
    /// </summary>
    private const long _DEFAULT_RATE = 1_000;

    #endregion

    #region Pricing

    [Test, Description("The need splits by what each source feeds rather than by its raw amount, or a thin ration would be drawn on as heavily as a rich one.")]
    public void ShouldSplitInProportionToNeedUnitsWhenSourcesArePricedDifferently()
    {
        // Equal amounts, but the berries are half as nourishing: 1,000 game feed 1,000 and 1,000 berries feed 500, so the need falls two to one on the game.
        FoodSource[] sources = [Biome(SustenanceResource.Hunt, _GAME_ASSET, available: 1_000), Patch(instanceId: 5, _BERRIES_ASSET, available: 1_000, rate: 2_000)];

        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(150), sources);

        using (Assert.EnterMultipleScope())
        {
            // 100 rations off the game at 1.0 and 50 off the berries at 2.0, which costs 100 berries.
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(100)));
            Assert.That(TakenFromPatch(extraction, instanceId: 5), Is.EqualTo(ScaledAmount.FromWhole(100)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.Zero));
        }
    }

    [Test, Description("Berries at their real 1.67 still feed exactly the need, the rounding falling on what the tile loses rather than on what the Forces eat.")]
    public void ShouldMeetTheNeedExactlyWhenAPriceDoesNotDivideCleanly()
    {
        FoodSource[] sources = [Biome(SustenanceResource.Hunt, _GAME_ASSET, available: 1_000), Patch(instanceId: 5, _BERRIES_ASSET, available: 1_000, rate: 1_670)];

        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), sources);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.NeedMet, Is.EqualTo(ScaledAmount.FromWhole(100)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.Zero));

            // The game feeds 1,000 and the berries 598.802, so the game bears the larger share of the hundred.
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Hunt), Is.GreaterThan(TakenFromPatch(extraction, instanceId: 5)));
            Assert.That(TakenFromPatch(extraction, instanceId: 5), Is.GreaterThan(ScaledAmount.Zero));
        }
    }

    [Test, Description("A patch priced beyond what one source can feed is still drawn on, rather than being dropped for contributing nothing whole.")]
    public void ShouldDrawOnASourceThatFeedsLessThanOneIndividual()
    {
        FoodSource[] sources = [Biome(SustenanceResource.Forage, _FORAGE_ASSET, available: 1_000), Patch(instanceId: 5, _BERRIES_ASSET, available: 1, rate: 2_000)];

        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(500), sources);

        Assert.That(extraction.NeedMet, Is.EqualTo(ScaledAmount.FromWhole(500)));
    }

    #endregion

    #region Splitting

    [Test, Description("The design doc's scenario: 400 Forage and 200 Hunt feeding 100 takes two thirds from Forage and one third from Hunt, the odd thousandth going to Forage.")]
    public void ShouldSplitTheNeedInProportionToWhatIsAvailable()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Land(forage: 400, hunt: 200));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromDecimal(66.667m)));
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromDecimal(33.333m)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.Zero));
        }
    }

    [TestCase(100_000, 400_000, 200_000, Description = "Two thirds and one third leave a thousandth over from rounding, which must still be taken.")]
    [TestCase(7, 1_000, 1_000, Description = "An even split of an odd number of thousandths hands the odd one to exactly one source.")]
    [TestCase(1_000, 2_000, 1_000, Description = "Thirds of a need recur, so rounding each share down alone would fall short of it.")]
    public void ShouldTakeExactlyTheNeedWhenTheTileCanCoverIt(long needRaw, long forageRaw, long huntRaw)
    {
        ScaledAmount need = ScaledAmount.FromRaw(needRaw);

        FoodSource[] sources =
        [
            Biome(SustenanceResource.Forage, _FORAGE_ASSET, ScaledAmount.FromRaw(forageRaw)),
            Biome(SustenanceResource.Hunt, _GAME_ASSET, ScaledAmount.FromRaw(huntRaw))
        ];

        FoodExtraction extraction = FoodExtractionCalculator.Extract(need, sources);

        Assert.That(extraction.NeedMet, Is.EqualTo(need));
    }

    [Test, Description("A source with very little available never gives more than it has, even when the rounding leftover would otherwise land on it.")]
    public void ShouldNeverTakeMoreOfASourceThanIsAvailable()
    {
        FoodSource[] sources =
        [
            Biome(SustenanceResource.Forage, _FORAGE_ASSET, ScaledAmount.FromRaw(1_000_000)),
            Biome(SustenanceResource.Hunt, _GAME_ASSET, ScaledAmount.FromRaw(1))
        ];

        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromRaw(999_999), sources);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Hunt), Is.LessThanOrEqualTo(ScaledAmount.FromRaw(1)));
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Forage), Is.LessThanOrEqualTo(ScaledAmount.FromRaw(1_000_000)));
            Assert.That(extraction.NeedMet, Is.EqualTo(ScaledAmount.FromRaw(999_999)));
        }
    }

    #endregion

    #region Shortfall

    [Test, Description("A tile holding less than the need is stripped bare, and the rest of the need is the shortfall.")]
    public void ShouldTakeEverythingAndReportTheShortfallWhenTheTileIsTooSmall()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Land(forage: 30, hunt: 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(30)));
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(10)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.FromWhole(60)));
        }
    }

    [Test, Description("A tile that exactly covers the need is taken in full with no shortfall, rather than falling into a split that could round.")]
    public void ShouldTakeEverythingWithNoShortfallWhenTheTileExactlyCoversTheNeed()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Land(forage: 60, hunt: 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(60)));
            Assert.That(TakenFromBiome(extraction, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(40)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.Zero));
        }
    }

    [Test, Description("A bare tile takes nothing and the whole need is the shortfall, which is the case a Force sitting on a stripped tile is in.")]
    public void ShouldTakeNothingWhenEverySourceIsBare()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Land(forage: 0, hunt: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.NeedMet, Is.EqualTo(ScaledAmount.Zero));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.FromWhole(100)));
        }
    }

    [Test, Description("A tile with no sources at all, as one whose biome has none of them is, takes nothing and the whole need is the shortfall.")]
    public void ShouldTakeNothingWhenThereAreNoSources()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.Taken, Is.Empty);
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.FromWhole(100)));
        }
    }

    #endregion

    #region Helpers

    private static FoodSource[] Land(long forage, long hunt)
    {
        return
        [
            Biome(SustenanceResource.Forage, _FORAGE_ASSET, available: forage),
            Biome(SustenanceResource.Hunt, _GAME_ASSET, available: hunt)
        ];
    }

    private static FoodSource Biome(SustenanceResource resource, int assetId, long available)
    {
        return Biome(resource, assetId, ScaledAmount.FromWhole(available));
    }

    private static FoodSource Biome(SustenanceResource resource, int assetId, ScaledAmount available)
    {
        return new FoodSource(new FoodSourceKey(FoodSourceType.Tile, resource, MapResourceInstanceId: null, assetId), available, _DEFAULT_RATE);
    }

    private static FoodSource Patch(int instanceId, int assetId, long available, long rate)
    {
        return new FoodSource(
            new FoodSourceKey(FoodSourceType.MapResource, Resource: null, instanceId, assetId),
            ScaledAmount.FromWhole(available),
            rate);
    }

    private static ScaledAmount TakenFromBiome(FoodExtraction extraction, SustenanceResource resource)
    {
        return extraction.Taken.Single(entry => entry.Key.Resource == resource).Value;
    }

    private static ScaledAmount TakenFromPatch(FoodExtraction extraction, int instanceId)
    {
        return extraction.Taken.Single(entry => entry.Key.MapResourceInstanceId == instanceId).Value;
    }

    #endregion
}
