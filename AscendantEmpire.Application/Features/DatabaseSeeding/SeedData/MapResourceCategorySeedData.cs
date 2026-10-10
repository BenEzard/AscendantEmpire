using AscendantEmpire.BackendContracts.MapResources.PutMapResourceCatalogue;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>map.MapResourceCategory</c> rows applied by the database seed: the groups the map editor lists MapResourceTypes under.
/// </summary>
/// <remarks>
/// <para>
/// Categories only. The types listed under them are seeded by <see cref="MapResourceTypeSeedData"/>, which runs after the Asset catalogue so that the
/// <c>assets.Asset</c> rows its yields, scenarios and layers name are there to point at. Any type that seed does not cover is made by hand in the
/// MapResource Type Manager.
/// </para>
/// <para>
/// Matched by code, so re-running the seed renames or re-orders a category rather than duplicating it, and never removes one added by hand.
/// </para>
/// </remarks>
internal static class MapResourceCategorySeedData
{
    /// <summary>
    /// Builds the category rows to seed, spaced by 10 so a category can later be slotted between two without renumbering.
    /// </summary>
    public static PutMapResourceCatalogueDto Build()
    {
        return new PutMapResourceCatalogueDto
        {
            Categories =
            [
                new AddOrUpdateMapResourceCategoryDto { Code = "food", DisplayName = "Food", SortOrder = 10 },
                new AddOrUpdateMapResourceCategoryDto { Code = "animals", DisplayName = "Animals", SortOrder = 20 },
                new AddOrUpdateMapResourceCategoryDto { Code = "wood", DisplayName = "Wood", SortOrder = 30 },
                new AddOrUpdateMapResourceCategoryDto { Code = "stone", DisplayName = "Stone", SortOrder = 40 },
                new AddOrUpdateMapResourceCategoryDto { Code = "metals", DisplayName = "Metals", SortOrder = 50 },
                new AddOrUpdateMapResourceCategoryDto { Code = "fuel", DisplayName = "Fuel", SortOrder = 60 },
                new AddOrUpdateMapResourceCategoryDto { Code = "luxuries", DisplayName = "Luxuries", SortOrder = 70 },
            ]
        };
    }
}
