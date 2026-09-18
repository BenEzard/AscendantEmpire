using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="ObserverSightRules"/> — no database, because the whole rule is a comparison and an addition.
/// </summary>
/// <remarks>
/// <para>
/// These pin the vision rules from <c>docs/design/campaign-map/vision.md</c>: a Biome that blocks sight caps how far an Observer can see, high ground adds
/// to it, and everything else leaves the Observer's own Sight alone.
/// </para>
/// <para>
/// The elevations used are the real seeded values — Flat 0, Hilly 1, Mountainous 3 — so a test that passes here is describing terrain that exists.
/// </para>
/// </remarks>
[TestFixture]
internal class ObserverSightRulesTests
{
    /// <summary>Relative elevation of Flat Geography, and of every sea-level Geography.</summary>
    private const int _FLAT_ELEVATION = 0;

    /// <summary>Relative elevation of Hilly Geography.</summary>
    private const int _HILLY_ELEVATION = 1;

    [Test, Description("A sight-blocking Biome takes an Observer's sight away outright, whatever its own Sight is worth.")]
    [TestCase(5, Description = "A far-seeing Observer is blinded like any other.")]
    [TestCase(1, Description = "A Settler's single tile of Sight goes the same way.")]
    [TestCase(0, Description = "An Observer with no Sight of its own is unaffected, because it had none to lose.")]
    public void ShouldZeroSightWhenTheOccupiedBiomeBlocksSight(int baseSightRange)
    {
        int resolved = ObserverSightRules.ResolveSightRange(
            baseSightRange: baseSightRange,
            occupiedTileBlocksSight: true,
            occupiedTileRelativeElevation: _FLAT_ELEVATION);

        Assert.That(resolved, Is.Zero, "A blinded Observer reports only the Tile it occupies; what it can still make out beside it is BlockedSightReveal's rule, not this one.");
    }

    [Test, Description("Blocking beats the elevation bonus: a Hilly Jungle is still a Jungle, and the extra height buys nothing through the canopy.")]
    public void ShouldLetTheBlockingBiomeBeatTheElevationBonus()
    {
        int resolved = ObserverSightRules.ResolveSightRange(
            baseSightRange: 4,
            occupiedTileBlocksSight: true,
            occupiedTileRelativeElevation: _HILLY_ELEVATION);

        Assert.That(resolved, Is.Zero);
    }

    [Test, Description("Ground at or below sea level is not a penalty, so an Observer on open flat terrain keeps exactly the Sight its template gives it.")]
    [TestCase(0, _FLAT_ELEVATION, Description = "A sightless Observer stays sightless.")]
    [TestCase(3, _FLAT_ELEVATION, Description = "Flat ground leaves Sight untouched.")]
    [TestCase(3, -2, Description = "A below-sea-level Geography is treated the same as Flat, not subtracted.")]
    public void ShouldLeaveSightUnchangedOnOpenGroundThatIsNotRaised(int baseSightRange, int occupiedTileRelativeElevation)
    {
        int resolved = ObserverSightRules.ResolveSightRange(
            baseSightRange: baseSightRange,
            occupiedTileBlocksSight: false,
            occupiedTileRelativeElevation: occupiedTileRelativeElevation);

        Assert.That(resolved, Is.EqualTo(baseSightRange));
    }

    [Test, Description("High ground adds its relative elevation to an Observer's Sight, which is the whole point of putting a watchtower on a hill.")]
    [TestCase(3, _HILLY_ELEVATION, 4, Description = "Hilly adds one.")]
    [TestCase(3, 3, 6, Description = "Mountainous adds three.")]
    [TestCase(0, _HILLY_ELEVATION, 1, Description = "The bonus applies even to an Observer with no Sight of its own.")]
    public void ShouldAddTheElevationBonusOnRaisedOpenGround(int baseSightRange, int occupiedTileRelativeElevation, int expectedSightRange)
    {
        int resolved = ObserverSightRules.ResolveSightRange(
            baseSightRange: baseSightRange,
            occupiedTileBlocksSight: false,
            occupiedTileRelativeElevation: occupiedTileRelativeElevation);

        Assert.That(resolved, Is.EqualTo(expectedSightRange));
    }
}
