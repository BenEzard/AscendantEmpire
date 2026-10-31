using AscendantEmpire.BackendContracts.MapResources.PutMapResourceCatalogue;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The MapResourceTypes applied by the database seed, collected from one <see cref="IMapResourceTypeSeed"/> per resource.
/// </summary>
/// <remarks>
/// <para>
/// <b>Adding a resource is a new file and one line of <see cref="_RESOURCES"/>.</b> Everything else - resolving its Assets, counting its rows, writing it -
/// already works off this list, and <c>MapResourceTypeSeedDataTests</c> holds every registered resource to the same invariants, so a new one is checked for
/// free.
/// </para>
/// <para>
/// Matched by code, so re-running the seed updates each resource's row rather than duplicating it, and leaves every type and category it does not name alone.
/// Within a type it is a different story: the catalogue handler treats each child collection as a replacement, so a resource's habitats, scenarios, layers
/// and yields here are the complete set, and anything added to a seeded type by hand is removed on the next run. A seeded resource's shape is owned by its
/// seed-data file.
/// </para>
/// <para>
/// Amounts and ceilings are <b>logical</b> units. The catalogue mapper scales them by <c>ScaledAmount.Scale</c> (1,000) on the way into the database, so no
/// seed-data file writes a scaled value.
/// </para>
/// </remarks>
internal static class MapResourceTypeSeedData
{
    /// <summary>
    /// Every resource the seed creates. Oil, Stone and the rest join this list as they are written.
    /// </summary>
    private static readonly IMapResourceTypeSeed[] _RESOURCES =
    [
        new TimberSeedData(),
        new BerriesSeedData()
    ];

    /// <summary>
    /// Every <c>assets.Asset</c> code the seeded resources name between them, for the seeding handler to resolve in one query before <see cref="Build"/>.
    /// </summary>
    /// <remarks>
    /// Deduplicated, since two resources may well draw on the same Asset.
    /// </remarks>
    public static IReadOnlyCollection<string> AssetCodes =>
        [.. _RESOURCES.SelectMany(resource => resource.AssetCodes).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// How many rows the seed writes across every resource, for the count beside the step's label.
    /// </summary>
    public static int ItemCount => _RESOURCES.Sum(resource => resource.ItemCount);

    /// <summary>
    /// How many resources the seed creates, for the step's completion summary.
    /// </summary>
    public static int ResourceCount => _RESOURCES.Length;

    /// <summary>
    /// Builds the catalogue payload holding every seeded resource.
    /// </summary>
    /// <remarks>
    /// No categories are sent. A category is this step's prerequisite rather than its business, and the catalogue handler refuses a type whose category it
    /// cannot find - which is the error worth seeing if the category seed has not run.
    /// </remarks>
    /// <param name="assetIdsByCode">Every code in <see cref="AssetCodes"/>, resolved to its <c>assets.Asset</c> id.</param>
    public static PutMapResourceCatalogueDto Build(IReadOnlyDictionary<string, int> assetIdsByCode)
    {
        return new PutMapResourceCatalogueDto
        {
            Types = [.. _RESOURCES.Select(resource => resource.Build(assetIdsByCode))]
        };
    }
}
