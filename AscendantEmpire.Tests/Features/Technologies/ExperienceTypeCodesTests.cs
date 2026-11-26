using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Technologies.Classes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests for <see cref="ExperienceTypeCodes"/> — the codes a producing system names when it earns experience.
/// </summary>
/// <remarks>
/// No database. The mapping is a pure function of the <see cref="BiomeType"/> member, and the codes it produces are held against
/// <c>ExperienceTypeSeedData</c>, which is the authority for which rows a seeded Game actually has.
/// </remarks>
[TestFixture]
internal class ExperienceTypeCodesTests
{
    #region Biome mapping

    [Test, Description("Every real terrain Biome resolves to the Biome.<Name> code the experience catalogue is seeded with, so a producing system never holds an id.")]
    [TestCase(BiomeType.Coastal, "Biome.Coastal")]
    [TestCase(BiomeType.Ocean, "Biome.Ocean")]
    [TestCase(BiomeType.Desert, "Biome.Desert")]
    [TestCase(BiomeType.Arid, "Biome.Arid")]
    [TestCase(BiomeType.Ice, "Biome.Ice")]
    [TestCase(BiomeType.Tundra, "Biome.Tundra")]
    [TestCase(BiomeType.Grassland, "Biome.Grassland")]
    [TestCase(BiomeType.Forest, "Biome.Forest")]
    [TestCase(BiomeType.Jungle, "Biome.Jungle")]
    [TestCase(BiomeType.Woodland, "Biome.Woodland")]
    public void ShouldResolveTheSeededCodeWhenTheBiomeIsRealTerrain(BiomeType biomeType, string expectedCode)
    {
        Assert.That(ExperienceTypeCodes.ForBiome(biomeType), Is.EqualTo(expectedCode));
    }

    [Test, Description("Unknown and Void are the absence of terrain rather than a kind of it, so they earn nothing and must not resolve to a code with no row behind it.")]
    [TestCase(BiomeType.Unknown)]
    [TestCase(BiomeType.Void)]
    public void ShouldResolveNothingWhenTheBiomeIsNotTerrain(BiomeType biomeType)
    {
        Assert.That(ExperienceTypeCodes.ForBiome(biomeType), Is.Null);
    }

    #endregion

    #region Catalogue agreement

    [Test, Description("Every code this class can produce has a seeded row behind it, because the allocator refuses a whole batch naming one it cannot resolve and a Turn's experience would be lost to a warning.")]
    public void EveryCodeShouldHaveASeededRow()
    {
        HashSet<string> seededCodes = [.. ExperienceTypeSeedData.Build().Select(experienceType => experienceType.Code)];

        string[] producible =
        [
            ExperienceTypeCodes.EXPLORATION,
            .. Enum.GetValues<BiomeType>().Select(ExperienceTypeCodes.ForBiome).OfType<string>()
        ];

        Assert.That(
            producible.Where(code => seededCodes.Contains(code) == false),
            Is.Empty,
            "A BiomeType added without a matching seed row silently costs every Player the whole Turn's biome experience.");
    }

    #endregion
}
