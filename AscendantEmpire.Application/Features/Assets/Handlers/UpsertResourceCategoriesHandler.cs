using System.Text.RegularExpressions;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Entities.Assets;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

public record UpsertResourceCategoriesRequest(
    IReadOnlyCollection<UpsertResourceMetaCategoryDto> MetaCategories) : IRequest<ServiceResultBase>;

internal class UpsertResourceCategoriesHandler(
    ILogger<UpsertResourceCategoriesHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<UpsertResourceCategoriesRequest, ServiceResultBase>
{
    private const int SortOrderStep = 10;

    public async Task<ServiceResultBase> Handle(
        UpsertResourceCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.MetaCategories.Count == 0)
                return Fail("At least one meta-category must be provided.");

            // --- Derive codes and sort orders from the request before touching the DB ---

            // Map each DTO to its derived code and sort order so validation and DB lookups
            // can work off the computed values without repeating the derivation logic.
            var metaPlan = request.MetaCategories
                .Select((m, i) => new
                {
                    Dto      = m,
                    Code     = ToProperCode(m.Name),
                    SortOrder = (i + 1) * SortOrderStep
                })
                .ToArray();

            // --- Uniqueness validation (request-level, before any DB work) ---

            List<string> errors = [];

            string[] duplicateMetaCodes = [.. metaPlan
                .GroupBy(m => m.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            if (duplicateMetaCodes.Length > 0)
                errors.Add($"Duplicate meta-category names resolve to the same code(s): {string.Join(", ", duplicateMetaCodes)}");

            // Build per-meta-category category plans so codes are hierarchical
            var categoryPlan = metaPlan
                .SelectMany(m => m.Dto.Categories
                    .Select((c, i) => new
                    {
                        Dto       = c,
                        MetaCode  = m.Code,
                        // e.g. "ConstructionGoods.Stone"
                        Code      = $"{m.Code}.{ToProperCode(c.Name)}",
                        SortOrder = (i + 1) * SortOrderStep
                    }))
                .ToArray();

            string[] duplicateCategoryCodes = [.. categoryPlan
                .GroupBy(c => c.Code)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            if (duplicateCategoryCodes.Length > 0)
                errors.Add($"Duplicate category names resolve to the same code(s): {string.Join(", ", duplicateCategoryCodes)}");

            if (errors.Count > 0)
                return Fail(errors);

            // --- Load existing records in bulk to avoid N+1 queries ---

            string[] metaCodes      = [.. metaPlan.Select(m => m.Code)];
            string[] categoryCodes  = [.. categoryPlan.Select(c => c.Code)];

            Dictionary<string, ResourceMetaCategory> existingMeta = await context.ResourceMetaCategories
                .Where(m => metaCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, cancellationToken);

            Dictionary<string, ResourceCategory> existingCategories = await context.ResourceCategories
                .Where(c => categoryCodes.Contains(c.Code))
                .ToDictionaryAsync(c => c.Code, cancellationToken);

            // --- Apply upserts ---

            foreach (var meta in metaPlan)
            {
                if (!existingMeta.TryGetValue(meta.Code, out ResourceMetaCategory? metaEntity))
                {
                    metaEntity = new ResourceMetaCategory { Code = meta.Code };
                    context.ResourceMetaCategories.Add(metaEntity);
                    existingMeta[meta.Code] = metaEntity;
                }

                metaEntity.Name      = meta.Dto.Name;
                metaEntity.SortOrder = meta.SortOrder;
                metaEntity.IconPath  = meta.Dto.IconPath;

                foreach (var cat in categoryPlan.Where(c => c.MetaCode == meta.Code))
                {
                    if (!existingCategories.TryGetValue(cat.Code, out ResourceCategory? catEntity))
                    {
                        catEntity = new ResourceCategory { Code = cat.Code };
                        // Link via navigation so EF resolves the FK before SaveChanges
                        metaEntity.Categories.Add(catEntity);
                        context.ResourceCategories.Add(catEntity);
                        existingCategories[cat.Code] = catEntity;
                    }
                    else
                    {
                        // Re-parent if the meta-category changed
                        catEntity.MetaCategory = metaEntity;
                    }

                    catEntity.Name      = cat.Dto.Name;
                    catEntity.SortOrder = cat.SortOrder;
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Upserted {MetaCount} meta-categories and {CatCount} categories.",
                metaPlan.Length,
                categoryPlan.Length);

            return SuccessNoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {Handler}.", nameof(UpsertResourceCategoriesHandler));
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
        // Split on any non-alpha run, title-case each word, then concatenate
        return string.Concat(
            Regex.Split(name.Trim(), @"[^a-zA-Z]+")
                 .Where(w => w.Length > 0)
                 .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }
}
