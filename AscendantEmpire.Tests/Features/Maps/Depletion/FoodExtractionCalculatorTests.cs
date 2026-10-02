using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="FoodExtractionCalculator"/>: how a food need is split across a tile's pool of resources, and what is left unmet.
/// </summary>
/// <remarks>
/// No database - the calculator is pure. Which resources make up the pool, and what is available of each, are gathering's to decide and are covered in <c>GatheringTests</c>.
/// </remarks>
[TestFixture]
internal class FoodExtractionCalculatorTests
{
    [Test, Description("The design doc's scenario: 400 Forage and 200 Hunt feeding 100 takes two thirds from Forage and one third from Hunt, with the odd thousandth going to Forage.")]
    public void ShouldSplitTheNeedInProportionToWhatIsAvailable()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Pool(forage: 400, hunt: 200));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.Taken[SustenanceResource.Forage], Is.EqualTo(ScaledAmount.FromDecimal(66.667m)));
            Assert.That(extraction.Taken[SustenanceResource.Hunt], Is.EqualTo(ScaledAmount.FromDecimal(33.333m)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.Zero));
        }
    }

    [TestCase(100_000, 400_000, 200_000, Description = "Two thirds and one third leave a thousandth over from rounding, which must still be taken.")]
    [TestCase(7, 1_000, 1_000, Description = "An even split of an odd number of thousandths hands the odd one to exactly one resource.")]
    [TestCase(1_000, 2_000, 1_000, Description = "Thirds of a need recur, so rounding each share down alone would fall short of it.")]
    public void ShouldTakeExactlyTheNeedWhenThePoolCanCoverIt(long needRaw, long forageRaw, long huntRaw)
    {
        ScaledAmount need = ScaledAmount.FromRaw(needRaw);

        var available = new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromRaw(forageRaw),
            [SustenanceResource.Hunt] = ScaledAmount.FromRaw(huntRaw)
        };

        FoodExtraction extraction = FoodExtractionCalculator.Extract(need, available);

        Assert.That(extraction.TotalTaken, Is.EqualTo(need));
    }

    [Test, Description("A resource with very little available never gives more than it has, even when the rounding leftover would otherwise land on it.")]
    public void ShouldNeverTakeMoreOfAResourceThanIsAvailable()
    {
        var available = new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromRaw(1_000_000),
            [SustenanceResource.Hunt] = ScaledAmount.FromRaw(1)
        };

        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromRaw(999_999), available);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.Taken[SustenanceResource.Hunt], Is.LessThanOrEqualTo(available[SustenanceResource.Hunt]));
            Assert.That(extraction.Taken[SustenanceResource.Forage], Is.LessThanOrEqualTo(available[SustenanceResource.Forage]));
            Assert.That(extraction.TotalTaken, Is.EqualTo(ScaledAmount.FromRaw(999_999)));
        }
    }

    [Test, Description("A pool smaller than the need is stripped bare, and the rest of the need is the shortfall.")]
    public void ShouldTakeEverythingAndReportTheShortfallWhenThePoolIsTooSmall()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Pool(forage: 30, hunt: 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.Taken[SustenanceResource.Forage], Is.EqualTo(ScaledAmount.FromWhole(30)));
            Assert.That(extraction.Taken[SustenanceResource.Hunt], Is.EqualTo(ScaledAmount.FromWhole(10)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.FromWhole(60)));
        }
    }

    [Test, Description("A pool that exactly covers the need is taken in full with no shortfall, rather than falling into a proportional split that could round.")]
    public void ShouldTakeEverythingWithNoShortfallWhenThePoolExactlyCoversTheNeed()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Pool(forage: 60, hunt: 40));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.Taken[SustenanceResource.Forage], Is.EqualTo(ScaledAmount.FromWhole(60)));
            Assert.That(extraction.Taken[SustenanceResource.Hunt], Is.EqualTo(ScaledAmount.FromWhole(40)));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.Zero));
        }
    }

    [Test, Description("A bare pool takes nothing and the whole need is the shortfall, which is the case a Force sitting on a stripped tile is in.")]
    public void ShouldTakeNothingWhenThePoolIsBare()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), Pool(forage: 0, hunt: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.TotalTaken, Is.EqualTo(ScaledAmount.Zero));
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.FromWhole(100)));
        }
    }

    [Test, Description("A pool with no resources at all, as on a tile whose biome has none of them, takes nothing and the whole need is the shortfall.")]
    public void ShouldTakeNothingWhenThePoolIsEmpty()
    {
        FoodExtraction extraction = FoodExtractionCalculator.Extract(ScaledAmount.FromWhole(100), new Dictionary<SustenanceResource, ScaledAmount>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(extraction.Taken, Is.Empty);
            Assert.That(extraction.Shortfall, Is.EqualTo(ScaledAmount.FromWhole(100)));
        }
    }

    /// <summary>
    /// A land pool of Forage and Hunt, in whole amounts.
    /// </summary>
    private static Dictionary<SustenanceResource, ScaledAmount> Pool(long forage, long hunt)
    {
        return new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromWhole(forage),
            [SustenanceResource.Hunt] = ScaledAmount.FromWhole(hunt)
        };
    }
}
