using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Validators;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.FrontendContracts.MapResources.PutMapResourceCatalogue;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record PutMapResourceCatalogueRequest(IPutMapResourceCatalogueDto Catalogue) : IRequest<ServiceResultBase>;

/// <summary>
/// Adds or updates MapResource categories and types, matched by code, in one save.
/// </summary>
/// <remarks>
/// <para>
/// Categories first, so a type can name a category created in the same request. Nothing absent from the request is deleted; see
/// <see cref="DeleteMapResourceTypeHandler"/> and <see cref="DeleteMapResourceCategoryHandler"/>.
/// </para>
/// <para>
/// Codes are matched case-insensitively, as the database's unique indexes compare them, so "berry" updates "Berry" rather than failing on the index.
/// </para>
/// <para>
/// A type's SortOrder is not in the request. The server gives a new type the next place in its category and never touches an existing one's, which is changed in the
/// database by hand.
/// </para>
/// </remarks>
internal class PutMapResourceCatalogueHandler(
    ILogger<PutMapResourceCatalogueHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<PutMapResourceCatalogueRequest, ServiceResultBase>
{
    /// <summary>
    /// The gap left between types created in the same category, so one can later be slotted between two by hand.
    /// </summary>
    private const int _SORT_ORDER_STEP = 10;

    public async Task<ServiceResultBase> Handle(PutMapResourceCatalogueRequest request, CancellationToken cancellationToken)
    {
        IPutMapResourceCatalogueDto catalogue = request.Catalogue;

        try
        {
            List<string> errors = ValidateRequest(catalogue);

            if (errors.Count != 0)
            {
                return Fail(errors);
            }

            // --- Categories ---------------------------------------------------------------
            // Few enough to read whole, and every one may be named by a type.
            Dictionary<string, MapResourceCategory> categoriesByCode = await context.MapResourceCategories
                .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (IAddOrUpdateMapResourceCategoryDto dto in catalogue.Categories)
            {
                if (categoriesByCode.TryGetValue(dto.Code, out MapResourceCategory? category) == false)
                {
                    category = new MapResourceCategory { Code = dto.Code, DisplayName = dto.DisplayName };
                    context.MapResourceCategories.Add(category);
                    categoriesByCode[dto.Code] = category;
                }

                category.Code = dto.Code;
                category.DisplayName = dto.DisplayName;
                category.SortOrder = dto.SortOrder;
            }

            // --- References the validator cannot check ------------------------------------
            foreach (IAddOrUpdateMapResourceTypeDto type in catalogue.Types.Where(t => categoriesByCode.ContainsKey(t.MapResourceCategoryCode) == false))
            {
                errors.Add($"{type.Code}: there is no category '{type.MapResourceCategoryCode}'.");
            }

            HashSet<int> assetIds = [.. catalogue.Types.SelectMany(ReferencedAssetIds)];
            HashSet<int> knownAssetIds = [.. await context.Assets
                .Where(r => assetIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken)];

            foreach (int missing in assetIds.Except(knownAssetIds).Order())
            {
                errors.Add($"There is no Asset {missing}.");
            }

            errors.AddRange(await WaterHabitatErrorsAsync(catalogue, cancellationToken));

            if (errors.Count != 0)
            {
                return Fail(errors);
            }

            // --- Types --------------------------------------------------------------------
            // Tracked with every child, so the mapper can update them in place.
            List<string> typeCodes = [.. catalogue.Types.Select(t => t.Code)];

            Dictionary<string, MapResourceType> typesByCode = await context.MapResourceTypes
                .AsSplitQuery()
                .Include(t => t.Yields)
                .Include(t => t.Habitats)
                .Include(t => t.DistributionScenarios).ThenInclude(s => s.Amounts)
                .Include(t => t.DistributionScenarios).ThenInclude(s => s.BiomeWeights)
                .Include(t => t.Layers)
                .Where(t => typeCodes.Contains(t.Code))
                .ToDictionaryAsync(t => t.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

            // The request does not carry SortOrder, so a new type is placed at the end of its category rather than left tied at zero with every other new type.
            // Keyed by category code, because a category created in this same request has no id yet.
            Dictionary<string, int> highestSortOrderByCategoryCode = await context.MapResourceTypes
                .Select(t => new { CategoryCode = t.Category.Code, t.SortOrder })
                .GroupBy(row => row.CategoryCode)
                .Select(group => new { CategoryCode = group.Key, Highest = group.Max(row => row.SortOrder) })
                .ToDictionaryAsync(row => row.CategoryCode, row => row.Highest, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (IAddOrUpdateMapResourceTypeDto dto in catalogue.Types)
            {
                if (typesByCode.TryGetValue(dto.Code, out MapResourceType? type) == false)
                {
                    type = new MapResourceType
                    {
                        Code = dto.Code,
                        DisplayName = dto.DisplayName,
                        SortOrder = NextSortOrder(highestSortOrderByCategoryCode, dto.MapResourceCategoryCode)
                    };

                    context.MapResourceTypes.Add(type);
                }

                MapResourceTypeMapper.Apply(dto, type, categoriesByCode[dto.MapResourceCategoryCode]);
            }

            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException ex)
        {
            // The one foreseeable way here: removing a Distribution scenario a map placement or a game instance still points at.
            const string error = "The MapResource catalogue could not be saved. A Distribution scenario that is removed must not be in use by any map or game.";
            logger.LogWarning(ex, error);
            return Fail([error]);
        }

        return ServiceResultBase.SuccessNoContent();
    }

    /// <summary>
    /// Where a newly created type sits in its category: one step past the highest already there.
    /// </summary>
    /// <remarks>
    /// Updates <paramref name="highestByCategoryCode"/> as it goes, so several types created into the same category in one request step apart rather than collide.
    /// </remarks>
    private static int NextSortOrder(Dictionary<string, int> highestByCategoryCode, string categoryCode)
    {
        int next = highestByCategoryCode.TryGetValue(categoryCode, out int highest) ? highest + _SORT_ORDER_STEP : _SORT_ORDER_STEP;
        highestByCategoryCode[categoryCode] = next;

        return next;
    }

    /// <summary>
    /// Every per-item rule, plus codes repeated within the request.
    /// </summary>
    private static List<string> ValidateRequest(IPutMapResourceCatalogueDto catalogue)
    {
        var categoryValidator = new MapResourceCategoryValidator();
        var typeValidator = new MapResourceTypeValidator();

        List<string> errors =
        [
            .. catalogue.Categories.SelectMany(c => categoryValidator.Validate(c).Errors).Select(e => e.ErrorMessage),
            .. catalogue.Types.SelectMany(t => typeValidator.Validate(t).Errors).Select(e => e.ErrorMessage),
        ];

        errors.AddRange(DuplicateCodes(catalogue.Categories.Select(c => c.Code)).Select(code => $"Category {code} is specified more than once."));
        errors.AddRange(DuplicateCodes(catalogue.Types.Select(t => t.Code)).Select(code => $"MapResourceType {code} is specified more than once."));

        return errors;
    }

    private static IEnumerable<string> DuplicateCodes(IEnumerable<string> codes)
    {
        return codes
            .Where(code => string.IsNullOrWhiteSpace(code) == false)
            .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
    }

    /// <summary>
    /// One error per habitat whose Biome or Geography disagrees with its type's <c>IsWaterResource</c>.
    /// </summary>
    private async Task<List<string>> WaterHabitatErrorsAsync(IPutMapResourceCatalogueDto catalogue, CancellationToken cancellationToken)
    {
        // Only the terrain this request actually names, which is a handful at most on either axis.
        HashSet<BiomeType> namedBiomes = [.. catalogue.Types.SelectMany(t => t.Habitats).Select(h => h.BiomeType)];

        HashSet<CampaignMapGeographyType> namedGeographies =
            [.. catalogue.Types.SelectMany(t => t.Habitats).Select(h => h.GeographyType).OfType<CampaignMapGeographyType>()];

        if (namedBiomes.Count == 0 && namedGeographies.Count == 0)
        {
            return [];
        }

        Dictionary<BiomeType, bool> isWaterByBiome = await context.CampaignBiomes
            .AsNoTracking()
            .Where(b => namedBiomes.Contains(b.BiomeTypeId))
            .ToDictionaryAsync(b => b.BiomeTypeId, b => b.IsWater, cancellationToken);

        Dictionary<CampaignMapGeographyType, bool> isWaterByGeography = await context.CampaignGeographies
            .AsNoTracking()
            .Where(g => namedGeographies.Contains(g.CampaignMapGeographyTypeId))
            .ToDictionaryAsync(g => g.CampaignMapGeographyTypeId, g => g.IsWater, cancellationToken);

        List<string> errors = [];

        foreach (IAddOrUpdateMapResourceTypeDto type in catalogue.Types)
        {
            foreach (BiomeType biome in type.Habitats.Select(h => h.BiomeType).Distinct())
            {
                if (DisagreesWithKind(isWaterByBiome, biome, type.IsWaterResource))
                {
                    errors.Add(WrongKindError(type.Code, biome.ToString(), type.IsWaterResource));
                }
            }

            foreach (CampaignMapGeographyType geography in type.Habitats.Select(h => h.GeographyType).OfType<CampaignMapGeographyType>().Distinct())
            {
                if (DisagreesWithKind(isWaterByGeography, geography, type.IsWaterResource))
                {
                    errors.Add(WrongKindError(type.Code, geography.ToString(), type.IsWaterResource));
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Whether terrain the catalogue holds is of the opposite water-ness to the type naming it.
    /// </summary>
    private static bool DisagreesWithKind<TTerrain>(Dictionary<TTerrain, bool> isWaterByTerrain, TTerrain terrain, bool isWaterResource) where TTerrain : notnull
    {
        return isWaterByTerrain.TryGetValue(terrain, out bool isWater) && isWater != isWaterResource;
    }

    /// <summary>
    /// The message for a habitat naming terrain of the wrong kind, phrased from the type's side so the author reads what to change.
    /// </summary>
    private static string WrongKindError(string typeCode, string terrain, bool isWaterResource)
    {
        return isWaterResource
            ? $"{typeCode}: {terrain} is not water, and a water MapResource can only live in water."
            : $"{typeCode}: {terrain} is water, and only a water MapResource can live there.";
    }

    /// <summary>
    /// Every Asset a type names: in its yields, its scenarios' amounts, and its layers.
    /// </summary>
    private static IEnumerable<int> ReferencedAssetIds(IAddOrUpdateMapResourceTypeDto type)
    {
        return type.Yields.Select(y => y.AssetId)
            .Concat(type.DistributionScenarios.SelectMany(s => s.Amounts).Select(a => a.AssetId))
            .Concat(type.Layers.Select(l => l.AssetId));
    }

    private static ServiceResultBase Fail(IEnumerable<string> errors)
    {
        return ServiceResultBase.Fail(errors);
    }
}
