using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="CampaignTileImpassability"/> — no database, because it is two comparisons over values a caller already holds.
/// </summary>
/// <remarks>
/// The rule matters twice over: movement refuses an impassable tile, and the territory pass treats one as a seal on an enclosure ring that carries no claim supply hop.
/// The last test here is the guard that keeps those the same fact rather than two copies of it.
/// </remarks>
[TestFixture]
internal class CampaignTileImpassabilityTests
{
    [Test, Description("Mountainous is seeded at exactly the impassable sentinel, so the comparison has to include it or every mountain becomes walkable.")]
    public void ShouldTreatTheSentinelCostAsImpassable()
    {
        Assert.That(CampaignTileImpassability.IsImpassable(biomeIsTraversable: true, CampaignGeography.IMPASSABLE_MOVEMENT_COST), Is.True);
    }

    [Test, Description("Expensive ground is not impassable ground, so a cost just under the sentinel is merely slow.")]
    public void ShouldTreatCostJustBelowTheSentinelAsPassable()
    {
        Assert.That(CampaignTileImpassability.IsImpassable(biomeIsTraversable: true, CampaignGeography.IMPASSABLE_MOVEMENT_COST - 1m), Is.False);
    }

    [Test, Description("The Void is impassable through its biome rather than its cost, which is why the rule reads both markers and not just the sentinel.")]
    public void ShouldTreatUntraversableBiomeAsImpassableOnOrdinaryGround()
    {
        Assert.That(CampaignTileImpassability.IsImpassable(biomeIsTraversable: false, geographyMovementCost: 1m), Is.True);
    }

    [Test, Description("Only the ground carries the sentinel, so an untraversable biome on cheap ground is not the geography's doing and a refusal must not name a mountain.")]
    public void ShouldNotCallUntraversableBiomeAGeographyRefusal()
    {
        Assert.That(CampaignTileImpassability.GeographyIsImpassable(geographyMovementCost: 1m), Is.False);
    }

    [TestCase(true, 1, Description = "Ordinary Flat ground is enterable.")]
    [TestCase(true, 999, Description = "A mountain is not.")]
    [TestCase(false, 1, Description = "Neither is the Void.")]
    [TestCase(false, 999, Description = "Nor both at once.")]
    [Test, Description("MovementTargetTile must agree with the shared rule for the same inputs, so the ground a Force is refused is exactly the ground that seals a border.")]
    public void ShouldAgreeWithMovementTargetTile(bool biomeIsTraversable, int geographyMovementCost)
    {
        MovementTargetTile target = new()
        {
            XCoord = 0,
            YCoord = 0,
            GeographyType = CampaignMapGeographyType.Flat,
            GeographyName = "Flat",
            GeographyMovementCost = geographyMovementCost,
            BiomeName = "Grassland",
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            BiomeIsTraversable = biomeIsTraversable
        };

        Assert.Multiple(() =>
        {
            Assert.That(target.IsImpassable, Is.EqualTo(CampaignTileImpassability.IsImpassable(biomeIsTraversable, geographyMovementCost)));
            Assert.That(target.GeographyIsImpassable, Is.EqualTo(CampaignTileImpassability.GeographyIsImpassable(geographyMovementCost)));
        });
    }
}
