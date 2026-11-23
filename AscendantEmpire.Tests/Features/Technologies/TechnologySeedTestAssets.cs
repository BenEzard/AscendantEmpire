using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Seeds the Assets the real technology catalogue names, for in-memory tests that upsert <c>TechnologySeedData</c>.
/// </summary>
/// <remarks>
/// The same dependency <c>TechnologySeedStep.DependsOn</c> declares on the Assets catalogue: <c>PrimitiveCrafting1</c>'s extraction efficiency grants name the
/// three generic material Assets, naturally occurring as the real Asset seed has them, and the upsert rejects the batch without them.
/// </remarks>
internal static class TechnologySeedTestAssets
{
    /// <summary>
    /// Adds the material Assets and returns their ids by resource.
    /// </summary>
    public static async Task<Dictionary<MaterialResource, int>> SeedMaterialAssetsAsync(AscendantEmpireContext context)
    {
        var metaCategory = new AssetMetaCategory { Code = "SeedMaterials", DisplayName = "Seed Materials", SortOrder = 90, IconPath = string.Empty };
        var category = new AssetCategory
        {
            Code = "SeedMaterials.Category",
            DisplayName = "Seed Materials Category",
            SortOrder = 90,
            CategoryType = AssetCategoryType.StandaloneAssets,
            MetaCategory = metaCategory
        };

        // Saved ahead of the Assets, because StorageClassId is required and so cannot wait for the navigation to be fixed up at SaveChanges.
        var storageClass = new StorageClass { Code = "SeedMaterials.Class", DisplayName = "Seed Materials Class" };
        context.StorageClasses.Add(storageClass);
        await context.SaveChangesAsync();

        foreach (string code in MaterialResourceAssets.CodesByResource.Values)
        {
            context.Assets.Add(new Asset
            {
                Code = code,
                DisplayName = code,
                IconPath = string.Empty,
                IsGradedAsset = false,
                IsNaturallyOccurring = true,
                SortOrder = 90,
                MetaCategory = metaCategory,
                Category = category,
                StorageClassId = storageClass.Id
            });
        }

        await context.SaveChangesAsync();

        string[] codes = [.. MaterialResourceAssets.CodesByResource.Values];

        Dictionary<string, int> idsByCode = await context.Assets
            .Where(asset => codes.Contains(asset.Code))
            .ToDictionaryAsync(asset => asset.Code, asset => asset.Id);

        return MaterialResourceAssets.CodesByResource.ToDictionary(entry => entry.Key, entry => idsByCode[entry.Value]);
    }
}
