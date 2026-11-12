using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Pins what a supply set write actually leaves in the database.
/// </summary>
/// <remarks>
/// Against a real SQL Server because that is the only tier where a store default exists: the in-memory provider has none, so a column default silently
/// rewriting a value an author chose could not surface there. Everything else about these handlers is covered in memory by
/// <see cref="SupplySetHandlerTests"/>.
/// </remarks>
[TestFixture]
internal class SupplySetPersistenceTests : SqlServerTestBase
{
    private const string _STEEL_PLATE = "ConstructionMaterials.Metal.SteelPlate";
    private const string _CIRCUITRY = "ConstructionMaterials.Metal.Circuitry";

    [Test, Description("A member weighted zero stays zero, which is how an Asset sits in a Repair set without ever being drawn.")]
    public async Task CreateSupplySet_PersistsAZeroSelectionWeight()
    {
        await SeedAssetsAsync(_STEEL_PLATE, _CIRCUITRY);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new CreateSupplySetHandler(new NullLogger<CreateSupplySetHandler>(), context);

        ServiceResult<SupplySetView> result = await handler.Handle(
            new CreateSupplySetRequest(new UpsertSupplySetDto
            {
                Code = "TankRepair",
                DisplayName = "Tank Repair",
                SupplyType = SupplyType.Repair,
                Assets =
                [
                    new UpsertSupplySetAssetDto { AssetCode = _STEEL_PLATE, AmountPerUnit = 1_000, SelectionWeight = 3 },
                    new UpsertSupplySetAssetDto { AssetCode = _CIRCUITRY, AmountPerUnit = 1_000, SelectionWeight = 0 },
                ]
            }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        Dictionary<string, int> weightsByAssetCode = await verifyContext.SupplySetAssets
            .Include(member => member.Asset)
            .ToDictionaryAsync(member => member.Asset.Code, member => member.SelectionWeight);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(weightsByAssetCode[_CIRCUITRY], Is.Zero, "An explicit zero must not be read as 'unset' and replaced by a default.");
            Assert.That(weightsByAssetCode[_STEEL_PLATE], Is.EqualTo(3));
        }
    }

    #region Helpers

    /// <summary>
    /// Seeds the Assets a set can name, with the one meta-category, category and storage class their non-null foreign keys demand.
    /// </summary>
    private async Task SeedAssetsAsync(params string[] codes)
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
            AssetMetaCategoryId = metaCategory.Id,
        };

        context.AssetCategories.Add(category);
        await context.SaveChangesAsync();

        int sortOrder = 0;

        foreach (string code in codes)
        {
            context.Assets.Add(new Asset
            {
                Code = code,
                DisplayName = code.Split('.').Last(),
                IconPath = "/resources/none.svg",
                IsGradedAsset = false,
                SortOrder = sortOrder++,
                AssetMetaCategoryId = metaCategory.Id,
                AssetCategoryId = category.Id,
                StorageClassId = storageClass.Id,
            });
        }

        await context.SaveChangesAsync();
    }

    #endregion
}
