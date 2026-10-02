using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="DepletionTaken"/>: that each resource reads and writes its own <c>*Taken</c> column.
/// </summary>
/// <remarks>
/// No database. Gathering writes through this and the tile toolbar reads through it, so a crossed column would show one resource's depletion against another.
/// </remarks>
[TestFixture]
internal class DepletionTakenTests
{
    [Test, Description("A tile with no depletion row has never been fed on, so nothing has been taken of any resource.")]
    public void ShouldReadZeroWhenTheTileHasNoDepletionRow()
    {
        Assert.That(DepletionTaken.GetTaken(null, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.Zero));
    }

    [TestCase(SustenanceResource.Forage, Description = "Forage reads its own column.")]
    [TestCase(SustenanceResource.Hunt, Description = "Hunt reads its own column.")]
    [TestCase(SustenanceResource.Fish, Description = "Fish reads its own column.")]
    [TestCase(SustenanceResource.Fodder, Description = "Fodder reads its own column.")]
    public void ShouldReadTheResourcesOwnColumn(SustenanceResource resource)
    {
        // A different amount in every column, so reading the wrong one gives a wrong number.
        var depletion = new CampaignMapDepletion { ForageTaken = 1_000, HuntTaken = 2_000, FishTaken = 3_000, FodderTaken = 4_000 };

        Assert.That(DepletionTaken.GetTaken(depletion, resource), Is.EqualTo(ScaledAmount.FromWhole((long)resource)));
    }

    [Test, Description("Adding to one resource accumulates on its own column and leaves every other resource untouched.")]
    public void ShouldAddOnlyToTheResourcesOwnColumn()
    {
        var depletion = new CampaignMapDepletion { HuntTaken = 5_000 };

        DepletionTaken.AddTaken(depletion, SustenanceResource.Hunt, ScaledAmount.FromWhole(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(depletion.HuntTaken, Is.EqualTo(7_000));
            Assert.That(depletion.ForageTaken, Is.Zero);
            Assert.That(depletion.FishTaken, Is.Zero);
            Assert.That(depletion.FodderTaken, Is.Zero);
        }
    }

    [Test, Description("Taking a negative amount is refused, because taking only ever adds and a negative Taken would break the tile's non-negative rule.")]
    public void ShouldRefuseToAddANegativeAmount()
    {
        var depletion = new CampaignMapDepletion();

        Assert.That(() => DepletionTaken.AddTaken(depletion, SustenanceResource.Forage, ScaledAmount.FromWhole(-1)), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
