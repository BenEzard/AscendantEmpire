using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests that the database enforces the two rules on <c>technology.TechnologyExtractionEfficiencyGrant</c>: one grant per Technology and Asset, and no grant that
/// changes nothing.
/// </summary>
/// <remarks>
/// Containerised because it is the constraints themselves under test, which <c>testing.md</c> says the in-memory provider cannot reproduce. The per-test database is restored
/// from the migrated template, so this also proves the migration created them.
/// </remarks>
[TestFixture]
internal class TechnologyExtractionEfficiencyGrantConstraintTests : SqlServerTestBase
{
    private int _firstTechnologyId;
    private int _secondTechnologyId;
    private int _rockAssetId;
    private int _logAssetId;

    #region Setup

    [SetUp]
    public async Task SeedTechnologiesAndAssetsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var metaCategory = new AssetMetaCategory { Code = "TestMeta", DisplayName = "Test Meta", SortOrder = 1, IconPath = "/resources/none.svg" };
        var storageClass = new StorageClass { Code = "TestStore", DisplayName = "Test Store" };

        context.AssetMetaCategories.Add(metaCategory);
        context.StorageClasses.Add(storageClass);
        await context.SaveChangesAsync();

        var category = new AssetCategory
        {
            Code = "TestMeta.Test",
            DisplayName = "Test",
            CategoryType = AssetCategoryType.StandaloneAssets,
            SortOrder = 1,
            AssetMetaCategoryId = metaCategory.Id
        };

        context.AssetCategories.Add(category);
        await context.SaveChangesAsync();

        Asset rock = BuildAsset("TestMeta.Rock", metaCategory.Id, category.Id, storageClass.Id, sortOrder: 1);
        Asset log = BuildAsset("TestMeta.Log", metaCategory.Id, category.Id, storageClass.Id, sortOrder: 2);
        Technology first = BuildTechnology("EfficiencyConstraintTechA", sortOrder: 10);
        Technology second = BuildTechnology("EfficiencyConstraintTechB", sortOrder: 11);

        context.Assets.AddRange(rock, log);
        context.Technologies.AddRange(first, second);
        await context.SaveChangesAsync();

        _rockAssetId = rock.Id;
        _logAssetId = log.Id;
        _firstTechnologyId = first.Id;
        _secondTechnologyId = second.Id;
    }

    #endregion

    #region Key

    [Test, Description("One Technology cannot grant the same Asset's efficiency twice, because a second row would be a second opinion on one figure.")]
    public async Task ShouldRejectWhenSameTechnologyAndAssetInsertedTwiceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologyExtractionEfficiencyGrants.Add(BuildGrant(_firstTechnologyId, _rockAssetId, flatValue: 950));
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        context.TechnologyExtractionEfficiencyGrants.Add(BuildGrant(_firstTechnologyId, _rockAssetId, flatValue: 200));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("Two Technologies may each grant the same Asset's efficiency, which is how later crafting tiers stack on PrimitiveCrafting1.")]
    public async Task ShouldAllowWhenSameAssetGrantedByDifferentTechnologiesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologyExtractionEfficiencyGrants.AddRange(
            BuildGrant(_firstTechnologyId, _rockAssetId, flatValue: 950),
            BuildGrant(_secondTechnologyId, _rockAssetId, flatValue: 200),
            BuildGrant(_firstTechnologyId, _logAssetId, flatValue: 950));

        await context.SaveChangesAsync();

        Assert.That(await context.TechnologyExtractionEfficiencyGrants.CountAsync(), Is.EqualTo(3));
    }

    #endregion

    #region Check constraint

    [Test, Description("A grant with neither a flat nor a percentage part is rejected, because it would change nothing while still using up the Technology's one row for that Asset.")]
    public async Task ShouldRejectWhenNeitherFlatNorPercentIsSetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologyExtractionEfficiencyGrants.Add(BuildGrant(_firstTechnologyId, _rockAssetId, flatValue: 0, percentValue: 0));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("A grant carrying only flat points is valid, which is the shape PrimitiveCrafting1 uses to lift the starting efficiency to full.")]
    public async Task ShouldAllowWhenOnlyFlatIsSetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologyExtractionEfficiencyGrants.Add(BuildGrant(_firstTechnologyId, _rockAssetId, flatValue: 950));

        await context.SaveChangesAsync();

        Assert.That(await context.TechnologyExtractionEfficiencyGrants.CountAsync(grant => grant.TechnologyId == _firstTechnologyId), Is.EqualTo(1));
    }

    [Test, Description("Negative values are accepted, because a Technology that makes an Asset harder to extract is a penalty rather than invalid data.")]
    public async Task ShouldAllowWhenValuesAreNegativeAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologyExtractionEfficiencyGrants.Add(BuildGrant(_firstTechnologyId, _rockAssetId, flatValue: -200));

        await context.SaveChangesAsync();

        TechnologyExtractionEfficiencyGrant stored = await context.TechnologyExtractionEfficiencyGrants.SingleAsync(grant => grant.TechnologyId == _firstTechnologyId);

        Assert.That(stored.FlatValue, Is.EqualTo(-200));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A grant with raw scaled values as the database holds them.
    /// </summary>
    private static TechnologyExtractionEfficiencyGrant BuildGrant(int technologyId, int assetId, long flatValue = 0, long percentValue = 0)
    {
        return new TechnologyExtractionEfficiencyGrant
        {
            TechnologyId = technologyId,
            AssetId = assetId,
            FlatValue = flatValue,
            PercentValue = percentValue
        };
    }

    private static Asset BuildAsset(string code, int metaCategoryId, int categoryId, int storageClassId, int sortOrder)
    {
        return new Asset
        {
            Code = code,
            DisplayName = code.Split('.').Last(),
            IconPath = "/resources/none.svg",
            IsGradedAsset = false,
            IsNaturallyOccurring = true,
            SortOrder = sortOrder,
            AssetMetaCategoryId = metaCategoryId,
            AssetCategoryId = categoryId,
            StorageClassId = storageClassId
        };
    }

    private static Technology BuildTechnology(string code, int sortOrder)
    {
        return new Technology
        {
            Code = code,
            DisplayName = code,
            IconPath = "/icons/tech.png",
            Era = TechnologyEra.Stone,
            SortOrder = sortOrder,
            DirectedRequired = 100
        };
    }

    #endregion
}
