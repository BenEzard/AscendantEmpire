using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="CampaignTileWater"/>: the one rule movement and gathering share for whether a tile is water.
/// </summary>
/// <remarks>
/// No database - the rule is pure.
/// </remarks>
[TestFixture]
internal class CampaignTileWaterTests
{
    [TestCase(true, CampaignMapGeographyType.DeepWater, true, Description = "A water biome on water geography is water.")]
    [TestCase(true, CampaignMapGeographyType.Flat, true, Description = "A water biome alone is enough, so a hand-edited map with land geography under the sea is still water.")]
    [TestCase(false, CampaignMapGeographyType.ShallowWater, true, Description = "Shallow water geography alone is enough, whatever biome is painted on it.")]
    [TestCase(false, CampaignMapGeographyType.DeepWater, true, Description = "Deep water geography alone is enough, whatever biome is painted on it.")]
    [TestCase(false, CampaignMapGeographyType.Flat, false, Description = "A land biome on land geography is land.")]
    public void ShouldTreatATileAsWaterWhenEitherTheBiomeOrTheGeographyIsWater(bool biomeIsWater, CampaignMapGeographyType geography, bool expectedIsWater)
    {
        Assert.That(CampaignTileWater.IsWater(biomeIsWater, geography), Is.EqualTo(expectedIsWater));
    }
}
