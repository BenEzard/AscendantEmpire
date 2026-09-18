using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Biomes.Validators;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="CampaignMapBiomeValidator"/>'s Regeneration Percent rules, and that the seeded biome catalogue satisfies every rule.
/// </summary>
/// <remarks>
/// No database - the validator is pure. The percentage (a fraction of Base) drives end-of-Round sustenance regrowth, so it is required - more than 0 and at most 1 - exactly
/// when a biome has a Forage, Hunt, Fish or Fodder value, and must be empty otherwise.
/// </remarks>
[TestFixture]
internal class CampaignMapBiomeValidatorTests
{
    private readonly CampaignMapBiomeValidator _validator = new();

    [TestCase(0.001, Description = "The smallest stored step, 0.1%, is allowed.")]
    [TestCase(0.5, Description = "A percentage within the range is allowed.")]
    [TestCase(1.0, Description = "The maximum, a whole Base back each Round, is itself allowed.")]
    public void ShouldAcceptAPercentWithinTheRangeOnABiomeWithSustenance(decimal regenerationPercent)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with { RegenerationPercent = regenerationPercent, BaseForageValue = 40 };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [TestCase(0.0, Description = "0 would never regrow anything.")]
    [TestCase(-0.1, Description = "A negative percentage would take food away.")]
    [TestCase(1.001, Description = "A thousandth above 1 would regrow more than a whole Base in a Round.")]
    [TestCase(1.25, Description = "An old-style multiplier is refused rather than read as 125%.")]
    public void ShouldRejectAPercentOutsideTheRangeOnABiomeWithSustenance(decimal regenerationPercent)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with { RegenerationPercent = regenerationPercent, BaseForageValue = 40 };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.Contains("must be more than 0 and at most 1"));
    }

    [TestCase(40, null, null, null, Description = "Forage alone needs a Regeneration Percent.")]
    [TestCase(null, 40, null, null, Description = "Hunt alone needs a Regeneration Percent.")]
    [TestCase(null, null, 40, null, Description = "Fish alone needs a Regeneration Percent.")]
    [TestCase(null, null, null, 40, Description = "Fodder alone needs a Regeneration Percent.")]
    [TestCase(0, null, null, null, Description = "A base of 0 still means the biome has the resource, so it still needs a Regeneration Percent.")]
    public void ShouldRejectANullPercentOnABiomeWithSustenance(int? forage, int? hunt, int? fish, int? fodder)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with
        {
            RegenerationPercent = null,
            BaseForageValue = forage,
            BaseHuntValue = hunt,
            BaseFishValue = fish,
            BaseFodderValue = fodder
        };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.Contains("must be more than 0 and at most 1"));
    }

    [TestCase(false, Description = "A biome with no resources at all has nothing to regrow, so a Regeneration Percent is refused.")]
    [TestCase(true, Description = "Bone, Wood and Stone are never depleted, so a biome with only material values has nothing to regrow either.")]
    public void ShouldRejectAPercentOnABiomeWithoutSustenance(bool hasMaterialValues)
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with
        {
            RegenerationPercent = 0.1m,
            BaseBoneValue = hasMaterialValues ? 20 : null,
            BaseWoodValue = hasMaterialValues ? 30 : null,
            BaseStoneValue = hasMaterialValues ? 50 : null
        };

        ValidationResult result = _validator.Validate(biome);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.Contains("must be empty"));
    }

    [Test, Description("A biome with no sustenance values and no Regeneration Percent is valid.")]
    public void ShouldAcceptANullPercentOnABiomeWithoutSustenance()
    {
        AddOrUpdateCampaignMapBiomeDto biome = LandBiome() with { RegenerationPercent = null, BaseStoneValue = 50 };

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

    /// <summary>A paintable, traversable land biome with no resources and no Regeneration Percent, which passes every rule, for each test to vary.</summary>
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
            RegenerationPercent = null,
            IsPaintableOnCampaignMap = true,
            MapEditorHtmlColor = "#7cb342",
            CanBeElevated = true
        };
    }
}
