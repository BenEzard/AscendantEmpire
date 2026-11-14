using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.Maps.Shared;

/// <summary>
/// Seeds the least a Force needs in order to eat: the Assets a biome's resources are, a food <c>SupplySet</c> holding them, and a design whose body
/// requires one ration of that set per individual.
/// </summary>
/// <remarks>
/// Shared by the fixtures that feed Forces or forecast tiles, because a Force whose design states no food need eats nothing at all and every one of their
/// expectations would then read zero. One ration per individual keeps their arithmetic the plain individuals-against-food it has always been.
/// </remarks>
internal static class FoodDesignSeed
{
    /// <summary>
    /// What a MapResource patch of berries yields, for the fixtures that put one on a tile.
    /// </summary>
    public const string BERRIES_ASSET_CODE = "Foodstuffs.PlantFoods.Berries";

    /// <summary>
    /// What the catalogue the fixtures share came out as.
    /// </summary>
    /// <param name="UnitTemplateId">The design Forces should be given: one ration per individual.</param>
    /// <param name="AssetIdsByCode">Every Asset seeded, so a fixture can point a MapResource yield at one.</param>
    internal sealed record FoodDesign(int UnitTemplateId, int SupplySetId, int BodyComponentId, IReadOnlyDictionary<string, int> AssetIdsByCode);

    /// <summary>
    /// Seeds the catalogue and one design that eats a ration per individual.
    /// </summary>
    /// <param name="amountPerUnit">What each Asset costs to feed one individual, raw fixed-point. The default of 1,000 is 1.0.</param>
    public static async Task<FoodDesign> SeedAsync(AscendantEmpireContext context, long amountPerUnit = 1_000)
    {
        var storageClass = new StorageClass { Code = "larder", DisplayName = "Larder" };
        context.StorageClasses.Add(storageClass);

        var metaCategory = new AssetMetaCategory { Code = "Foodstuffs", DisplayName = "Foodstuffs", SortOrder = 1, IconPath = "/icon.png" };
        context.AssetMetaCategories.Add(metaCategory);

        await context.SaveChangesAsync();

        string[] codes = [.. SustenanceResourceAssets.CodesByResource.Values, BERRIES_ASSET_CODE];

        Asset[] assets =
        [
            .. codes.Select((code, index) => new Asset
            {
                Code = code,
                DisplayName = code[(code.LastIndexOf('.') + 1)..],
                IconPath = "/icon.png",
                IsGradedAsset = false,
                SortOrder = index + 1,
                AssetMetaCategoryId = metaCategory.Id,
                StorageClassId = storageClass.Id
            })
        ];

        context.Assets.AddRange(assets);

        var supplySet = new SupplySet { Code = "human-food", DisplayName = "Human Food", SupplyType = SupplyType.Food };
        context.SupplySets.Add(supplySet);

        var body = new UnitComponent { Code = "test_body", DisplayName = "Test Body", ComponentType = ComponentType.Body };
        context.UnitComponents.Add(body);

        await context.SaveChangesAsync();

        context.SupplySetAssets.AddRange(assets.Select(asset => new SupplySetAsset
        {
            SupplySetId = supplySet.Id,
            AssetId = asset.Id,
            AmountPerUnit = amountPerUnit
        }));

        await context.SaveChangesAsync();

        int templateId = await AddDesignAsync(context, supplySet.Id, body.Id, foodQuantity: 1_000);

        return new FoodDesign(templateId, supplySet.Id, body.Id, assets.ToDictionary(asset => asset.Code, asset => asset.Id));
    }

    /// <summary>
    /// Adds another design on the same body and set, eating <paramref name="foodQuantity"/> per individual, and returns its template id.
    /// </summary>
    /// <param name="foodQuantity">Rations per individual per Turn, raw fixed-point. Null is a design that states no food need at all.</param>
    public static async Task<int> AddDesignAsync(AscendantEmpireContext context, int? supplySetId, int bodyComponentId, long? foodQuantity)
    {
        var template = new UnitTemplate { DisplayName = "Test Design", MapImagePath = "/unit.png", BodyComponentId = bodyComponentId };
        context.UnitTemplates.Add(template);

        await context.SaveChangesAsync();

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = template.Id,
            DisplayName = template.DisplayName,
            MapImagePath = template.MapImagePath,
            BodyComponentId = bodyComponentId,
            MovementDomain = DomainType.Land,
            FoodSupplySetId = foodQuantity is null ? null : supplySetId,
            FoodQuantity = foodQuantity
        });

        await context.SaveChangesAsync();

        return template.Id;
    }
}
