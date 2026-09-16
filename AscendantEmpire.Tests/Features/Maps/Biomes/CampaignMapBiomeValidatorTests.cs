using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Biomes.Validators;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="CampaignMapBiomeValidator"/>'s Regeneration Rate rules, and that the seeded biome catalogue satisfies every rule.
/// </summary>
/// <remarks>
/// No database - the validator is pure. The rate drives end-of-Round sustenance regrowth, so it is required (at least 1.05) exactly when a biome has a Forage, Hunt, Fish
/// or Fodder value, and must be empty otherwise.
/// </remarks>
[TestFixture]
internal class CampaignMapBiomeValidatorTests
{
    private readonly CampaignMapBiomeValidator _validator = new();

    [TestCase(1.05, Description = "The minimum rate is itself allowed.")]
    [TestCase(1.5, Description = "Any rate above the minimum is allowed.")]
    public void ShouldAcceptARateAtOrAboveTheMinimumOnABiomeWithSustenance(decimal regenerationRate)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with { RegenerationRate = regenerationRate, BaseForageValue = 40 };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [TestCase(1.049, Description = "A thousandth below the minimum is refused.")]
    [TestCase(1.0, Description = "A rate of 1 would never regrow anything.")]
    public void ShouldRejectARateBelowTheMinimumOnABiomeWithSustenance(decimal regenerationRate)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with { RegenerationRate = regenerationRate, BaseForageValue = 40 };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.Contains("must be at least 1.05"));
    }

    [TestCase(40, null, null, null, Description = "Forage alone needs a rate.")]
    [TestCase(null, 40, null, null, Description = "Hunt alone needs a rate.")]
    [TestCase(null, null, 40, null, Description = "Fish alone needs a rate.")]
    [TestCase(null, null, null, 40, Description = "Fodder alone needs a rate.")]
    [TestCase(0, null, null, null, Description = "A base of 0 still means the biome has the resource, so it still needs a rate.")]
    public void ShouldRejectANullRateOnABiomeWithSustenance(int? forage, int? hunt, int? fish, int? fodder)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with
        {
            RegenerationRate = null,
            BaseForageValue = forage,
            BaseHuntValue = hunt,
            BaseFishValue = fish,
            BaseFodderValue = fodder
        };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.Contains("must be at least 1.05"));
    }

    [TestCase(false, Description = "A biome with no resources at all has nothing to regrow, so a rate is refused.")]
    [TestCase(true, Description = "Bone, Wood and Stone are never depleted, so a biome with only material values has nothing to regrow either.")]
    public void ShouldRejectARateOnABiomeWithoutSustenance(bool hasMaterialValues)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with
        {
            RegenerationRate = 1.1m,
            BaseBoneValue = hasMaterialValues ? 20 : null,
            BaseWoodValue = hasMaterialValues ? 30 : null,
            BaseStoneValue = hasMaterialValues ? 50 : null
        };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.Contains("must be empty"));
    }

    [Test, Description("A biome with no sustenance values and no rate is valid.")]
    public void ShouldAcceptANullRateOnABiomeWithoutSustenance()
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with { RegenerationRate = null, BaseStoneValue = 50 };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Test, Description("Every seeded biome passes the validator, so running the database seed can never fail on its own catalogue.")]
    public void ShouldAcceptEverySeededBiome()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (AddOrUpdateCampaignMapBiomeDto biome in BiomeSeedData.Build())
            {
                ValidationResult result = _validator.Validate(biome);

                Assert.That(result.IsValid, Is.True, $"{biome.BiomeType}: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
            }
        }
    }

    /// <summary>A paintable, traversable land biome with no resources and no rate, which passes every rule, for each test to vary.</summary>
    private static AddOrUpdateCampaignMapBiomeDto LandBiome()
    {
        return new AddOrUpdateCampaignMapBiomeDto
        {
            BiomeType = BiomeType.Grassland,
            Name = "Grassland",
            MovementCost = 1m,
            DoesBlockSight = false,
            IsWater = false,
            IsTraversable = true,
            RegenerationRate = null,
            IsPaintableOnCampaignMap = true,
            MapEditorHtmlColor = "#7cb342",
            CanBeElevated = true
        };
    }
}
