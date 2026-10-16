using System.Text.RegularExpressions;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

public record UpsertAssetsRequest(IReadOnlyCollection<UpsertAssetMetaCategoryDto> MetaCategories) : IRequest<ServiceResultBase>;

internal class UpsertAssetsHandler(
    ILogger<UpsertAssetsHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpsertAssetsRequest, ServiceResultBase>
{
    private const int _SORT_ORDER_STEP = 10;

    public async Task<ServiceResultBase> Handle(
        UpsertAssetsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MetaCategories.Count == 0)
        {
            return Fail("At least one meta-category must be provided.");
        }

        // --- Step 1: Derive all codes and sort orders from the request before touching the DB.
        //     Working with immutable plan objects avoids repeating derivation logic and keeps validation independent of DB state. ---

        // A supplied Code is taken whole at every level rather than being prefixed by its parent, so the code a caller writes is exactly the code that
        // lands in the database and can be grepped for. Codes are the upsert's match key: derived-only would mean renaming a DisplayName inserted a
        // second row instead of renaming the first.
        var metaPlan = request.MetaCategories
            .Select((m, i) => new
            {
                Dto = m,
                Code = m.Code ?? ToProperCode(m.DisplayName),
                SortOrder = m.SortOrder ?? (i + 1) * _SORT_ORDER_STEP
            })
            .ToArray();

        // Derived category codes are hierarchical: "MetaCode.CategoryCode"
        var categoryPlan = metaPlan
            .SelectMany(m => m.Dto.Categories
                .Select((c, i) => new
                {
                    Dto = c,
                    MetaCode = m.Code,
                    Code = c.Code ?? $"{m.Code}.{ToProperCode(c.DisplayName)}",
                    SortOrder = c.SortOrder ?? (i + 1) * _SORT_ORDER_STEP
                }))
            .ToArray();

        // Derived group codes: "MetaCode.CategoryCode.GroupCode" (grouped categories only)
        var groupPlan = categoryPlan
            .Where(c => c.Dto.CategoryType == AssetCategoryType.GroupedAssets)
            .SelectMany(c => c.Dto.Groups
                .Select((g, i) => new
                {
                    Dto = g,
                    CategoryCode = c.Code,
                    MetaCode = c.MetaCode,
                    Code = g.Code ?? $"{c.Code}.{ToProperCode(g.DisplayName)}",
                    SortOrder = g.SortOrder ?? (i + 1) * _SORT_ORDER_STEP
                }))
            .ToArray();

        // Assets via the grouped path (belong to an AssetGroup)
        var groupedAssetPlan = groupPlan
            .SelectMany(g => g.Dto.Assets
                .Select((r, i) => new
                {
                    Dto = r,
                    GroupCode = (string?)g.Code,
                    CategoryCode = (string?)null,
                    MetaCode = g.MetaCode,
                    Code = r.Code ?? $"{g.Code}.{ToProperCode(r.DisplayName)}",
                    SortOrder = r.SortOrder ?? (i + 1) * _SORT_ORDER_STEP
                }))
            .ToArray();

        // Assets via the standalone path (belong directly to an AssetCategory)
        var standaloneAssetPlan = categoryPlan
            .Where(c => c.Dto.CategoryType == AssetCategoryType.StandaloneAssets)
            .SelectMany(c => c.Dto.Assets
                .Select((r, i) => new
                {
                    Dto = r,
                    GroupCode = (string?)null,
                    CategoryCode = (string?)c.Code,
                    MetaCode = c.MetaCode,
                    Code = r.Code ?? $"{c.Code}.{ToProperCode(r.DisplayName)}",
                    SortOrder = r.SortOrder ?? (i + 1) * _SORT_ORDER_STEP
                }))
            .ToArray();

        // --- Step 2: Uniqueness validation (request-level, before any DB work) ---

        List<string> errors = [];

        string[] duplicateMetaCodes = [.. metaPlan
            .GroupBy(m => m.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (duplicateMetaCodes.Length > 0)
        {
            errors.Add($"Duplicate meta-category names resolve to the same code(s): {string.Join(", ", duplicateMetaCodes)}");
        }

        string[] duplicateCategoryCodes = [.. categoryPlan
            .GroupBy(c => c.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (duplicateCategoryCodes.Length > 0)
        {
            errors.Add($"Duplicate category names resolve to the same code(s): {string.Join(", ", duplicateCategoryCodes)}");
        }

        string[] duplicateGroupCodes = [.. groupPlan
            .GroupBy(g => g.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (duplicateGroupCodes.Length > 0)
        {
            errors.Add($"Duplicate group names resolve to the same code(s): {string.Join(", ", duplicateGroupCodes)}");
        }

        string[] allAssetCodes = [..
            groupedAssetPlan.Select(r => r.Code)
                .Concat(standaloneAssetPlan.Select(r => r.Code))];

        string[] duplicateAssetCodes = [.. allAssetCodes
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (duplicateAssetCodes.Length > 0)
        {
            errors.Add($"Duplicate asset names resolve to the same code(s): {string.Join(", ", duplicateAssetCodes)}");
        }

        if (errors.Count > 0)
        {
            return Fail(errors);
        }

        // --- Step 3: Load all existing records in bulk to avoid N+1 queries ---

        string[] metaCodes = [.. metaPlan.Select(m => m.Code)];
        string[] categoryCodes = [.. categoryPlan.Select(c => c.Code)];
        string[] groupCodes = [.. groupPlan.Select(g => g.Code)];

        Dictionary<string, AssetMetaCategory> existingMeta = await context.AssetMetaCategories
            .Where(m => metaCodes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, cancellationToken);

        Dictionary<string, AssetCategory> existingCategories = await context.AssetCategories
            .Where(c => categoryCodes.Contains(c.Code))
            .ToDictionaryAsync(c => c.Code, cancellationToken);

        Dictionary<string, AssetGroup> existingGroups = groupCodes.Length > 0
            ? await context.AssetGroups
                .Where(g => groupCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code, cancellationToken)
            : [];

        Dictionary<string, Asset> existingAssets = allAssetCodes.Length > 0
            ? await context.Assets
                .Where(r => allAssetCodes.Contains(r.Code))
                .ToDictionaryAsync(r => r.Code, cancellationToken)
            : [];

        // --- Step 4: Upsert MetaCategories ---

        foreach (var meta in metaPlan)
        {
            if (!existingMeta.TryGetValue(meta.Code, out AssetMetaCategory? metaEntity))
            {
                metaEntity = new AssetMetaCategory
                {
                    Code = meta.Code,
                    DisplayName = meta.Dto.DisplayName,
                    SortOrder = meta.SortOrder,
                    IconPath = meta.Dto.IconPath ?? string.Empty
                };
                context.AssetMetaCategories.Add(metaEntity);
                existingMeta[meta.Code] = metaEntity;
            }
            else
            {
                metaEntity.DisplayName = meta.Dto.DisplayName;
                metaEntity.SortOrder = meta.SortOrder;

                // Only overwrite IconPath when explicitly provided, to allow partial updates.
                if (meta.Dto.IconPath is not null)
                {
                    metaEntity.IconPath = meta.Dto.IconPath;
                }
            }
        }

        // --- Step 5: Upsert Categories ---

        foreach (var cat in categoryPlan)
        {
            AssetMetaCategory metaEntity = existingMeta[cat.MetaCode];

            if (!existingCategories.TryGetValue(cat.Code, out AssetCategory? catEntity))
            {
                catEntity = new AssetCategory
                {
                    Code = cat.Code,
                    DisplayName = cat.Dto.DisplayName,
                    SortOrder = cat.SortOrder,
                    CategoryType = cat.Dto.CategoryType,
                    SubjectScope = cat.Dto.SubjectScope
                };
                // Link via navigation so EF resolves the FK before SaveChanges.
                metaEntity.Categories.Add(catEntity);
                context.AssetCategories.Add(catEntity);
                existingCategories[cat.Code] = catEntity;
            }
            else
            {
                catEntity.DisplayName = cat.Dto.DisplayName;
                catEntity.SortOrder = cat.SortOrder;
                catEntity.CategoryType = cat.Dto.CategoryType;
                catEntity.SubjectScope = cat.Dto.SubjectScope;
                catEntity.MetaCategory = metaEntity;
            }
        }

        // --- Step 6: Upsert Groups ---

        foreach (var grp in groupPlan)
        {
            AssetCategory catEntity = existingCategories[grp.CategoryCode];

            if (!existingGroups.TryGetValue(grp.Code, out AssetGroup? groupEntity))
            {
                groupEntity = new AssetGroup
                {
                    Code = grp.Code,
                    DisplayName = grp.Dto.DisplayName,
                    SortOrder = grp.SortOrder,
                    Category = catEntity
                };
                context.AssetGroups.Add(groupEntity);
                existingGroups[grp.Code] = groupEntity;
            }
            else
            {
                groupEntity.DisplayName = grp.Dto.DisplayName;
                groupEntity.SortOrder = grp.SortOrder;
                groupEntity.Category = catEntity;
            }
        }

        // --- Step 7: Upsert Assets ---

        foreach (var res in groupedAssetPlan)
        {
            AssetMetaCategory metaEntity = existingMeta[res.MetaCode];
            AssetGroup groupEntity = existingGroups[res.GroupCode!];

            if (!existingAssets.TryGetValue(res.Code, out Asset? assetEntity))
            {
                assetEntity = new Asset
                {
                    Code = res.Code,
                    DisplayName = res.Dto.DisplayName,
                    IconPath = res.Dto.IconPath ?? string.Empty,
                    Description = res.Dto.Description,
                    SortOrder = res.SortOrder,
                    IsGradedAsset = res.Dto.IsGradedAsset,
                    AssetGrade = res.Dto.AssetGrade,
                    NeverLuxury = res.Dto.NeverLuxury,
                    MetaCategory = metaEntity,
                    Group = groupEntity
                };
                context.Assets.Add(assetEntity);
                existingAssets[res.Code] = assetEntity;
            }
            else
            {
                assetEntity.DisplayName = res.Dto.DisplayName;
                assetEntity.SortOrder = res.SortOrder;
                assetEntity.IsGradedAsset = res.Dto.IsGradedAsset;
                assetEntity.AssetGrade = res.Dto.AssetGrade;
                assetEntity.NeverLuxury = res.Dto.NeverLuxury;
                assetEntity.MetaCategory = metaEntity;

                // Ensure grouped path is set and standalone path is cleared.
                assetEntity.Group = groupEntity;
                assetEntity.Category = null;

                // IconPath and Description are only overwritten when supplied, to allow partial updates.
                if (res.Dto.IconPath is not null)
                {
                    assetEntity.IconPath = res.Dto.IconPath;
                }

                if (res.Dto.Description is not null)
                {
                    assetEntity.Description = res.Dto.Description;
                }
            }
        }

        foreach (var res in standaloneAssetPlan)
        {
            AssetMetaCategory metaEntity = existingMeta[res.MetaCode];
            AssetCategory catEntity = existingCategories[res.CategoryCode!];

            if (!existingAssets.TryGetValue(res.Code, out Asset? assetEntity))
            {
                assetEntity = new Asset
                {
                    Code = res.Code,
                    DisplayName = res.Dto.DisplayName,
                    IconPath = res.Dto.IconPath ?? string.Empty,
                    Description = res.Dto.Description,
                    SortOrder = res.SortOrder,
                    IsGradedAsset = res.Dto.IsGradedAsset,
                    AssetGrade = res.Dto.AssetGrade,
                    NeverLuxury = res.Dto.NeverLuxury,
                    MetaCategory = metaEntity,
                    Category = catEntity
                };
                context.Assets.Add(assetEntity);
                existingAssets[res.Code] = assetEntity;
            }
            else
            {
                assetEntity.DisplayName = res.Dto.DisplayName;
                assetEntity.SortOrder = res.SortOrder;
                assetEntity.IsGradedAsset = res.Dto.IsGradedAsset;
                assetEntity.AssetGrade = res.Dto.AssetGrade;
                assetEntity.NeverLuxury = res.Dto.NeverLuxury;
                assetEntity.MetaCategory = metaEntity;

                // Ensure standalone path is set and grouped path is cleared.
                assetEntity.Category = catEntity;
                assetEntity.Group = null;

                // IconPath and Description are only overwritten when supplied, to allow partial updates.
                if (res.Dto.IconPath is not null)
                {
                    assetEntity.IconPath = res.Dto.IconPath;
                }

                if (res.Dto.Description is not null)
                {
                    assetEntity.Description = res.Dto.Description;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Upserted {MetaCount} meta-categories, {CatCount} categories, {GroupCount} groups, {AssetCount} assets.",
            metaPlan.Length,
            categoryPlan.Length,
            groupPlan.Length,
            groupedAssetPlan.Length + standaloneAssetPlan.Length);

        return SuccessNoContent();
    }

    /// <summary>
    /// Normalises a display name into a ProperCase code with all non-alpha characters removed.
    /// Each word is title-cased and whitespace/punctuation is stripped.
    /// e.g. "Construction Goods" -> "ConstructionGoods", "stone &amp; wood" -> "StoneWood"
    /// </summary>
    private static string ToProperCode(string name)
    {
        // Split on any non-alpha run, title-case each word, then concatenate.
        return string.Concat(
            Regex.Split(name.Trim(), @"[^a-zA-Z]+")
                 .Where(w => w.Length > 0)
                 .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }
}
