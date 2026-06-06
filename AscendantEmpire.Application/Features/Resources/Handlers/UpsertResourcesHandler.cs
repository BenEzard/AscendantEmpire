using System.Text.RegularExpressions;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Resources;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Enums.Resources;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Resources.Handlers;

public record UpsertResourcesRequest(IReadOnlyCollection<UpsertResourceMetaCategoryDto> MetaCategories) : IRequest<ServiceResultBase>;

internal class UpsertResourcesHandler(
    ILogger<UpsertResourcesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpsertResourcesRequest, ServiceResultBase>
{
    private const int SortOrderStep = 10;

    public async Task<ServiceResultBase> Handle(
        UpsertResourcesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.MetaCategories.Count == 0)
                return Fail("At least one meta-category must be provided.");

            // --- Step 1: Derive all codes and sort orders from the request before touching the DB.
            //     Working with immutable plan objects avoids repeating derivation logic and keeps
            //     validation independent of DB state. ---

            var metaPlan = request.MetaCategories
                .Select((m, i) => new
                {
                    Dto = m,
                    Code = ToProperCode(m.DisplayName),
                    SortOrder = m.SortOrder ?? (i + 1) * SortOrderStep
                })
                .ToArray();

            // Category codes are hierarchical: "MetaCode.CategoryCode"
            var categoryPlan = metaPlan
                .SelectMany(m => m.Dto.Categories
                    .Select((c, i) => new
                    {
                        Dto = c,
                        MetaCode = m.Code,
                        Code = $"{m.Code}.{ToProperCode(c.DisplayName)}",
                        SortOrder = c.SortOrder ?? (i + 1) * SortOrderStep
                    }))
                .ToArray();

            // Group codes: "MetaCode.CategoryCode.GroupCode" (grouped categories only)
            var groupPlan = categoryPlan
                .Where(c => c.Dto.CategoryType == ResourceCategoryType.GroupedResources)
                .SelectMany(c => c.Dto.Groups
                    .Select((g, i) => new
                    {
                        Dto = g,
                        CategoryCode = c.Code,
                        MetaCode = c.MetaCode,
                        Code = $"{c.Code}.{ToProperCode(g.DisplayName)}",
                        SortOrder = g.SortOrder ?? (i + 1) * SortOrderStep
                    }))
                .ToArray();

            // Resources via the grouped path (belong to a ResourceGroup)
            var groupedResourcePlan = groupPlan
                .SelectMany(g => g.Dto.Resources
                    .Select(r => new
                    {
                        Dto = r,
                        GroupCode = (string?)g.Code,
                        CategoryCode = (string?)null,
                        MetaCode = g.MetaCode,
                        Code = r.Code ?? $"{g.Code}.{ToProperCode(r.DisplayName)}"
                    }))
                .ToArray();

            // Resources via the standalone path (belong directly to a ResourceCategory)
            var standaloneResourcePlan = categoryPlan
                .Where(c => c.Dto.CategoryType == ResourceCategoryType.StandaloneResources)
                .SelectMany(c => c.Dto.Resources
                    .Select(r => new
                    {
                        Dto = r,
                        GroupCode = (string?)null,
                        CategoryCode = (string?)c.Code,
                        MetaCode = c.MetaCode,
                        Code = r.Code ?? $"{c.Code}.{ToProperCode(r.DisplayName)}"
                    }))
                .ToArray();

            // --- Step 2: Uniqueness validation (request-level, before any DB work) ---

            List<string> errors = [];

            string[] duplicateMetaCodes = [.. metaPlan
                .GroupBy(m => m.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            if (duplicateMetaCodes.Length > 0)
                errors.Add($"Duplicate meta-category names resolve to the same code(s): {string.Join(", ", duplicateMetaCodes)}");

            string[] duplicateCategoryCodes = [.. categoryPlan
                .GroupBy(c => c.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            if (duplicateCategoryCodes.Length > 0)
                errors.Add($"Duplicate category names resolve to the same code(s): {string.Join(", ", duplicateCategoryCodes)}");

            string[] duplicateGroupCodes = [.. groupPlan
                .GroupBy(g => g.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            if (duplicateGroupCodes.Length > 0)
                errors.Add($"Duplicate group names resolve to the same code(s): {string.Join(", ", duplicateGroupCodes)}");

            string[] allResourceCodes = [..
                groupedResourcePlan.Select(r => r.Code)
                    .Concat(standaloneResourcePlan.Select(r => r.Code))];

            string[] duplicateResourceCodes = [.. allResourceCodes
                .GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            if (duplicateResourceCodes.Length > 0)
                errors.Add($"Duplicate resource names resolve to the same code(s): {string.Join(", ", duplicateResourceCodes)}");

            if (errors.Count > 0)
                return Fail(errors);

            // --- Step 3: Load all existing records in bulk to avoid N+1 queries ---

            string[] metaCodes = [.. metaPlan.Select(m => m.Code)];
            string[] categoryCodes = [.. categoryPlan.Select(c => c.Code)];
            string[] groupCodes = [.. groupPlan.Select(g => g.Code)];

            Dictionary<string, ResourceMetaCategory> existingMeta = await context.ResourceMetaCategories
                .Where(m => metaCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, cancellationToken);

            Dictionary<string, ResourceCategory> existingCategories = await context.ResourceCategories
                .Where(c => categoryCodes.Contains(c.Code))
                .ToDictionaryAsync(c => c.Code, cancellationToken);

            Dictionary<string, ResourceGroup> existingGroups = groupCodes.Length > 0
                ? await context.ResourceGroups
                    .Where(g => groupCodes.Contains(g.Code))
                    .ToDictionaryAsync(g => g.Code, cancellationToken)
                : [];

            Dictionary<string, Resource> existingResources = allResourceCodes.Length > 0
                ? await context.Resources
                    .Where(r => allResourceCodes.Contains(r.Code))
                    .ToDictionaryAsync(r => r.Code, cancellationToken)
                : [];

            // --- Step 4: Upsert MetaCategories ---

            foreach (var meta in metaPlan)
            {
                if (!existingMeta.TryGetValue(meta.Code, out ResourceMetaCategory? metaEntity))
                {
                    metaEntity = new ResourceMetaCategory
                    {
                        Code = meta.Code,
                        DisplayName = meta.Dto.DisplayName,
                        SortOrder = meta.SortOrder,
                        IconPath = meta.Dto.IconPath ?? string.Empty
                    };
                    context.ResourceMetaCategories.Add(metaEntity);
                    existingMeta[meta.Code] = metaEntity;
                }
                else
                {
                    metaEntity.DisplayName = meta.Dto.DisplayName;
                    metaEntity.SortOrder = meta.SortOrder;
                    // Only overwrite IconPath when explicitly provided, to allow partial updates.
                    if (meta.Dto.IconPath is not null)
                        metaEntity.IconPath = meta.Dto.IconPath;
                }
            }

            // --- Step 5: Upsert Categories ---

            foreach (var cat in categoryPlan)
            {
                ResourceMetaCategory metaEntity = existingMeta[cat.MetaCode];

                if (!existingCategories.TryGetValue(cat.Code, out ResourceCategory? catEntity))
                {
                    catEntity = new ResourceCategory
                    {
                        Code = cat.Code,
                        DisplayName = cat.Dto.DisplayName,
                        SortOrder = cat.SortOrder,
                        CategoryType = cat.Dto.CategoryType
                    };
                    // Link via navigation so EF resolves the FK before SaveChanges.
                    metaEntity.Categories.Add(catEntity);
                    context.ResourceCategories.Add(catEntity);
                    existingCategories[cat.Code] = catEntity;
                }
                else
                {
                    catEntity.DisplayName = cat.Dto.DisplayName;
                    catEntity.SortOrder = cat.SortOrder;
                    catEntity.CategoryType = cat.Dto.CategoryType;
                    catEntity.MetaCategory = metaEntity;
                }
            }

            // --- Step 6: Upsert Groups ---

            foreach (var grp in groupPlan)
            {
                ResourceCategory catEntity = existingCategories[grp.CategoryCode];

                if (!existingGroups.TryGetValue(grp.Code, out ResourceGroup? groupEntity))
                {
                    groupEntity = new ResourceGroup
                    {
                        Code = grp.Code,
                        DisplayName = grp.Dto.DisplayName,
                        SortOrder = grp.SortOrder,
                        Category = catEntity
                    };
                    context.ResourceGroups.Add(groupEntity);
                    existingGroups[grp.Code] = groupEntity;
                }
                else
                {
                    groupEntity.DisplayName = grp.Dto.DisplayName;
                    groupEntity.SortOrder = grp.SortOrder;
                    groupEntity.Category = catEntity;
                }
            }

            // --- Step 7: Upsert Resources ---

            foreach (var res in groupedResourcePlan)
            {
                ResourceMetaCategory metaEntity = existingMeta[res.MetaCode];
                ResourceGroup groupEntity = existingGroups[res.GroupCode!];

                if (!existingResources.TryGetValue(res.Code, out Resource? resourceEntity))
                {
                    resourceEntity = new Resource
                    {
                        Code = res.Code,
                        DisplayName = res.Dto.DisplayName,
                        IconPath = res.Dto.IconPath ?? string.Empty,
                        IsGradedResource = res.Dto.IsGradedResource,
                        NeverLuxury = res.Dto.NeverLuxury,
                        MetaCategory = metaEntity,
                        Group = groupEntity
                    };
                    context.Resources.Add(resourceEntity);
                    existingResources[res.Code] = resourceEntity;
                }
                else
                {
                    resourceEntity.DisplayName = res.Dto.DisplayName;
                    resourceEntity.IsGradedResource = res.Dto.IsGradedResource;
                    resourceEntity.NeverLuxury = res.Dto.NeverLuxury;
                    resourceEntity.MetaCategory = metaEntity;
                    // Ensure grouped path is set and standalone path is cleared.
                    resourceEntity.Group = groupEntity;
                    resourceEntity.Category = null;
                    if (res.Dto.IconPath is not null)
                        resourceEntity.IconPath = res.Dto.IconPath;
                }
            }

            foreach (var res in standaloneResourcePlan)
            {
                ResourceMetaCategory metaEntity = existingMeta[res.MetaCode];
                ResourceCategory catEntity = existingCategories[res.CategoryCode!];

                if (!existingResources.TryGetValue(res.Code, out Resource? resourceEntity))
                {
                    resourceEntity = new Resource
                    {
                        Code = res.Code,
                        DisplayName = res.Dto.DisplayName,
                        IconPath = res.Dto.IconPath ?? string.Empty,
                        IsGradedResource = res.Dto.IsGradedResource,
                        NeverLuxury = res.Dto.NeverLuxury,
                        MetaCategory = metaEntity,
                        Category = catEntity
                    };
                    context.Resources.Add(resourceEntity);
                    existingResources[res.Code] = resourceEntity;
                }
                else
                {
                    resourceEntity.DisplayName = res.Dto.DisplayName;
                    resourceEntity.IsGradedResource = res.Dto.IsGradedResource;
                    resourceEntity.NeverLuxury = res.Dto.NeverLuxury;
                    resourceEntity.MetaCategory = metaEntity;
                    // Ensure standalone path is set and grouped path is cleared.
                    resourceEntity.Category = catEntity;
                    resourceEntity.Group = null;
                    if (res.Dto.IconPath is not null)
                        resourceEntity.IconPath = res.Dto.IconPath;
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Upserted {MetaCount} meta-categories, {CatCount} categories, {GroupCount} groups, {ResourceCount} resources.",
                metaPlan.Length,
                categoryPlan.Length,
                groupPlan.Length,
                groupedResourcePlan.Length + standaloneResourcePlan.Length);

            return SuccessNoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {Handler}.", nameof(UpsertResourcesHandler));
            return InternalServerError(ex.Message);
        }
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
