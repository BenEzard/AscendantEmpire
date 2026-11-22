using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests that the database enforces the two rules on <c>technology.TechnologySustenanceModifierGrant</c>: one grant per Technology, resource and biome, and no grant that
/// changes nothing.
/// </summary>
/// <remarks>
/// <para>
/// The unique index relies on <c>HasFilter(null)</c>, which EF's SQL Server convention would otherwise quietly replace with an <c>IS NOT NULL</c> filter that lets one
/// Technology hold any number of all-biome grants for the same resource.
/// </para>
/// <para>
/// Containerised because it is the constraints themselves under test, which <c>testing.md</c> says the in-memory provider cannot reproduce. The per-test database is restored
/// from the migrated template, so this also proves the migration created them.
/// </para>
/// </remarks>
[TestFixture]
internal class TechnologySustenanceModifierGrantConstraintTests : SqlServerTestBase
{
    private int _technologyId;

    #region Setup

    [SetUp]
    public async Task SeedTechnologyAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Biomes are seeded by the Entity Setup Tool rather than by a migration, so the template database may not have the ones a biome-specific grant names.
        foreach (BiomeType biomeType in (BiomeType[])[BiomeType.Arid, BiomeType.Grassland])
        {
            if (!await context.CampaignBiomes.AnyAsync(biome => biome.BiomeTypeId == biomeType))
            {
                context.CampaignBiomes.Add(new CampaignBiome
                {
                    BiomeTypeId = biomeType,
                    Name = biomeType.ToString(),
                    MovementCost = 1m,
                    IsTraversable = true,
                    MapEditorHtmlColor = "#7cb342"
                });
            }
        }

        var technology = new Technology
        {
            Code = "GrantConstraintTech",
            DisplayName = "Grant Constraint Tech",
            IconPath = "/icons/tech.png",
            Era = TechnologyEra.Stone,
            SortOrder = 10,
            DirectedRequired = 100
        };

        context.Technologies.Add(technology);
        await context.SaveChangesAsync();

        _technologyId = technology.Id;
    }

    #endregion

    #region Unique index

    [Test, Description("One Technology cannot grant the same resource in the same biome twice, because a second row would be a second opinion on one figure.")]
    public async Task ShouldRejectWhenSameTechnologyResourceAndBiomeInsertedTwiceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, percentValue: 200));
        await context.SaveChangesAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, percentValue: 300));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("One Technology cannot hold two all-biome grants for the same resource, because NULL biomes must collide in the unique index rather than be filtered out of it.")]
    public async Task ShouldRejectWhenSameTechnologyAndResourceGrantedForAllBiomesTwiceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, biome: null, percentValue: 200));
        await context.SaveChangesAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, biome: null, percentValue: 300));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("The same resource may be granted in two different biomes, since those modify different values.")]
    public async Task ShouldAllowWhenSameResourceGrantedInDifferentBiomesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.AddRange(
            BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, percentValue: 200),
            BuildGrant(SustenanceResource.Hunt, BiomeType.Grassland, percentValue: 200));

        await context.SaveChangesAsync();

        Assert.That(await context.TechnologySustenanceModifierGrants.CountAsync(grant => grant.TechnologyId == _technologyId), Is.EqualTo(2));
    }

    [Test, Description("Two resources may be granted in the same biome, which is what a biome skill raising both Hunt and Forage needs.")]
    public async Task ShouldAllowWhenSameBiomeGrantedForDifferentResourcesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.AddRange(
            BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, percentValue: 200),
            BuildGrant(SustenanceResource.Forage, BiomeType.Arid, percentValue: 200));

        await context.SaveChangesAsync();

        Assert.That(await context.TechnologySustenanceModifierGrants.CountAsync(grant => grant.TechnologyId == _technologyId), Is.EqualTo(2));
    }

    [Test, Description("An all-biome grant and a biome-specific grant for one resource may coexist, since they modify different sets of values.")]
    public async Task ShouldAllowWhenAllBiomesAndBiomeSpecificGrantedForSameResourceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.AddRange(
            BuildGrant(SustenanceResource.Hunt, biome: null, percentValue: 200),
            BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, percentValue: 300));

        await context.SaveChangesAsync();

        Assert.That(await context.TechnologySustenanceModifierGrants.CountAsync(grant => grant.TechnologyId == _technologyId), Is.EqualTo(2));
    }

    #endregion

    #region Check constraint

    [Test, Description("A grant with neither a flat nor a percentage part is rejected, because it would change nothing while still using up the Technology's one row for that resource and biome.")]
    public async Task ShouldRejectWhenNeitherFlatNorPercentIsSetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, flatValue: 0, percentValue: 0));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("A grant carrying only a flat amount is valid, so a Technology can add food outright rather than as a share of the base.")]
    public async Task ShouldAllowWhenOnlyFlatIsSetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, flatValue: 5_000, percentValue: 0));

        await context.SaveChangesAsync();

        Assert.That(await context.TechnologySustenanceModifierGrants.CountAsync(grant => grant.TechnologyId == _technologyId), Is.EqualTo(1));
    }

    [Test, Description("A grant carrying only a percentage is valid, which is the shape every biome skill uses.")]
    public async Task ShouldAllowWhenOnlyPercentIsSetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, flatValue: 0, percentValue: 200));

        await context.SaveChangesAsync();

        Assert.That(await context.TechnologySustenanceModifierGrants.CountAsync(grant => grant.TechnologyId == _technologyId), Is.EqualTo(1));
    }

    [Test, Description("Negative values are accepted, because a Technology that makes a resource harder to gather is a penalty rather than invalid data.")]
    public async Task ShouldAllowWhenValuesAreNegativeAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(BuildGrant(SustenanceResource.Hunt, BiomeType.Arid, flatValue: -1_000, percentValue: -200));

        await context.SaveChangesAsync();

        TechnologySustenanceModifierGrant stored = await context.TechnologySustenanceModifierGrants.SingleAsync(grant => grant.TechnologyId == _technologyId);

        Assert.Multiple(() =>
        {
            Assert.That(stored.FlatValue, Is.EqualTo(-1_000));
            Assert.That(stored.PercentValue, Is.EqualTo(-200));
        });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A grant from the fixture's one Technology, with raw scaled values as the database holds them.
    /// </summary>
    private TechnologySustenanceModifierGrant BuildGrant(SustenanceResource resource, BiomeType? biome, long flatValue = 0, long percentValue = 0)
    {
        return new TechnologySustenanceModifierGrant
        {
            TechnologyId = _technologyId,
            Resource = resource,
            BiomeTypeId = biome,
            FlatValue = flatValue,
            PercentValue = percentValue
        };
    }

    #endregion
}
