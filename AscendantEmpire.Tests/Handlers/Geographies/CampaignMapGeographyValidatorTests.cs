using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Geographies.Validators;
using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Handlers.Geographies;

/// <summary>
/// Tier 1 tests for <see cref="CampaignMapGeographyValidator"/> — it touches no DbContext, so the rules are exercised directly.
/// </summary>
/// <remarks>
/// The traversability rule is deliberately one-directional. An impassable <see cref="CampaignGeography.MovementCost"/> must mean untraversable, but an
/// untraversable Geography need not carry that cost: Unknown is untraversable because it is a data gap, not because the ground is expensive, exactly as the
/// Void biome is untraversable with an ordinary cost.
/// </remarks>
internal class CampaignMapGeographyValidatorTests
{
    /// <summary>
    /// A Geography that passes every rule, for a test to spoil one value of.
    /// </summary>
    private static AddOrUpdateCampaignMapGeographyDto AValidGeography()
    {
        return new AddOrUpdateCampaignMapGeographyDto
        {
            CampaignMapGeographyType = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };
    }

    private static ServiceResultBase Validate(AddOrUpdateCampaignMapGeographyDto geography)
    {
        return CampaignMapGeographyValidator.ValidateGeographies(null!, [geography]);
    }

    [Test, Description("Pins that water geography cannot be elevated, the same rule tile painting already enforces, so the catalogue cannot hold a combination the map editor would refuse.")]
    public void ShouldFailWhenAWaterGeographyIsElevated()
    {
        AddOrUpdateCampaignMapGeographyDto geography = AValidGeography();
        geography.CampaignMapGeographyType = CampaignMapGeographyType.ShallowWater;
        geography.Name = "Shallow Water";
        geography.IsWater = true;
        geography.RelativeElevation = 1;

        ServiceResultBase result = Validate(geography);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "An elevated water Geography was accepted.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected an error naming the elevation.");
        }
    }

    [Test, Description("Pins that a water geography at sea level passes, so the elevation rule rejects only what it is aimed at.")]
    public void ShouldSucceedWhenAWaterGeographyIsAtSeaLevel()
    {
        AddOrUpdateCampaignMapGeographyDto geography = AValidGeography();
        geography.CampaignMapGeographyType = CampaignMapGeographyType.DeepWater;
        geography.Name = "Deep Water";
        geography.IsWater = true;
        geography.RelativeElevation = -2;

        ServiceResultBase result = Validate(geography);

        Assert.That(result.HasSucceeded, Is.True, $"A water Geography below sea level was rejected: {string.Join("; ", result.Errors ?? [])}");
    }

    [Test, Description("Pins that a traversable Geography cannot carry the impassable cost, since the flag is what decides the question and the sentinel survives only as data.")]
    public void ShouldFailWhenATraversableGeographyCarriesTheImpassableCost()
    {
        AddOrUpdateCampaignMapGeographyDto geography = AValidGeography();
        geography.CampaignMapGeographyType = CampaignMapGeographyType.Mountainous;
        geography.Name = "Mountainous";
        geography.MovementCost = CampaignGeography.IMPASSABLE_MOVEMENT_COST;
        geography.IsTraversable = true;

        ServiceResultBase result = Validate(geography);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "A traversable Geography carrying the impassable cost was accepted.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected an error naming the contradiction.");
        }
    }

    [Test, Description("Pins that an untraversable Geography may carry an ordinary cost, because Unknown is untraversable as a data gap rather than as expensive ground.")]
    public void ShouldSucceedWhenAnUntraversableGeographyCarriesAnOrdinaryCost()
    {
        AddOrUpdateCampaignMapGeographyDto geography = AValidGeography();
        geography.CampaignMapGeographyType = CampaignMapGeographyType.Unknown;
        geography.Name = "Unknown";
        geography.MovementCost = 1m;
        geography.IsTraversable = false;
        geography.IsPaintableOnCampaignMap = false;

        ServiceResultBase result = Validate(geography);

        Assert.That(result.HasSucceeded, Is.True, $"An untraversable Geography with an ordinary cost was rejected: {string.Join("; ", result.Errors ?? [])}");
    }

    [Test, Description("Pins that the catalogue the seed ships passes its own validator, which is what would catch a rule written tighter than the data it has to admit.")]
    public void ShouldSucceedForEverySeededGeography()
    {
        ServiceResultBase result = CampaignMapGeographyValidator.ValidateGeographies(null!, [.. GeographySeedData.Build()]);

        Assert.That(result.HasSucceeded, Is.True, $"The seeded Geographies were rejected: {string.Join("; ", result.Errors ?? [])}");
    }
}
