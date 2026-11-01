using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="CampaignTileTerrain"/> — no database, because every rule is a read of flags the caller already holds.
/// </summary>
/// <remarks>
/// Impassability matters twice over: movement refuses the tile, and the territory pass treats it as a seal on an enclosure ring that carries no claim supply
/// hop. The last test is the guard that keeps those the same fact rather than two copies of it.
/// </remarks>
[TestFixture]
internal class CampaignTileTerrainTests
{
    [TestCase(true, true, true, Description = "A water biome on water geography is water.")]
    [TestCase(true, false, true, Description = "A water biome alone is enough, so a hand-edited map with land geography under the sea is still water.")]
    [TestCase(false, true, true, Description = "Water geography alone is enough, whatever biome is painted on it.")]
    [TestCase(false, false, false, Description = "A land biome on land geography is land.")]
    public void ShouldTreatATileAsWaterWhenEitherTheBiomeOrTheGeographyIsWater(bool biomeIsWater, bool geographyIsWater, bool expectedIsWater)
    {
        Assert.That(CampaignTileTerrain.IsWater(biomeIsWater, geographyIsWater), Is.EqualTo(expectedIsWater));
    }

    [Test, Description("Mountainous is seeded untraversable, so the geography flag alone has to refuse the tile however ordinary the biome painted on it is.")]
    public void ShouldTreatUntraversableGeographyAsImpassable()
    {
        Assert.That(CampaignTileTerrain.IsImpassable(biomeIsTraversable: true, geographyIsTraversable: false), Is.True);
    }

    [Test, Description("Expensive ground is not impassable ground, so traversable ground under a traversable biome is enterable whatever it costs to cross.")]
    public void ShouldTreatTraversableGroundUnderATraversableBiomeAsPassable()
    {
        Assert.That(CampaignTileTerrain.IsImpassable(biomeIsTraversable: true, geographyIsTraversable: true), Is.False);
    }

    [Test, Description("The Void is impassable through its biome rather than its ground, which is why the rule reads both flags and not just the geography's.")]
    public void ShouldTreatUntraversableBiomeAsImpassableOnOrdinaryGround()
    {
        Assert.That(CampaignTileTerrain.IsImpassable(biomeIsTraversable: false, geographyIsTraversable: true), Is.True);
    }

    [Test, Description("Both markers at once is still one refusal, so a Void painted on a mountain reads impassable rather than falling through the disjunction.")]
    public void ShouldTreatBothMarkersAtOnceAsImpassable()
    {
        Assert.That(CampaignTileTerrain.IsImpassable(biomeIsTraversable: false, geographyIsTraversable: false), Is.True);
    }

    [Test, Description("Only the ground answers this one, so an untraversable biome on enterable ground is not the geography's doing and a refusal must not name a mountain.")]
    public void ShouldNotCallUntraversableBiomeAGeographyRefusal()
    {
        Assert.That(CampaignTileTerrain.GeographyIsImpassable(geographyIsTraversable: true), Is.False);
    }

    [Test, Description("Untraversable ground is the geography's doing, which is what lets a refusal name the mountain instead of using the Void's general wording.")]
    public void ShouldCallUntraversableGeographyAGeographyRefusal()
    {
        Assert.That(CampaignTileTerrain.GeographyIsImpassable(geographyIsTraversable: false), Is.True);
    }

    [TestCase(true, true, Description = "Ordinary Flat ground is enterable.")]
    [TestCase(true, false, Description = "A mountain is not.")]
    [TestCase(false, true, Description = "Neither is the Void.")]
    [TestCase(false, false, Description = "Nor both at once.")]
    public void ShouldAgreeWithMovementTargetTile(bool biomeIsTraversable, bool geographyIsTraversable)
    {
        MovementTargetTile target = new()
        {
            XCoord = 0,
            YCoord = 0,
            GeographyType = CampaignMapGeographyType.Flat,
            GeographyName = "Flat",
            GeographyMovementCost = 1m,
            GeographyIsTraversable = geographyIsTraversable,
            BiomeName = "Grassland",
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            GeographyIsWater = false,
            BiomeIsTraversable = biomeIsTraversable
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.IsImpassable, Is.EqualTo(CampaignTileTerrain.IsImpassable(biomeIsTraversable, geographyIsTraversable)));
            Assert.That(target.GeographyIsImpassable, Is.EqualTo(CampaignTileTerrain.GeographyIsImpassable(geographyIsTraversable)));
        }
    }
}
