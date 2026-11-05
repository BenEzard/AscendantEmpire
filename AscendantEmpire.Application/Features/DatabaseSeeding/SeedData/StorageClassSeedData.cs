using AscendantEmpire.BackendContracts.Assets;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>assets.StorageClass</c> catalogue applied by the database seed: how each Asset is kept, as against what it is.
/// </summary>
/// <remarks>
/// <para>
/// Matched by code, so re-running the seed renames a row rather than duplicating it, and never removes one added by hand. Codes are spelled out explicitly
/// rather than derived from the display name: <c>AssetSeedData</c> names every class through the constants below, so a reworded display name must not shift
/// a code and quietly re-file a catalogue's worth of stock.
/// </para>
/// <para>
/// <b>Breadth is chosen per class, not per level of the asset hierarchy, and classes do not nest.</b> That is the whole reason this is a catalogue of its own
/// rather than a column on <c>AssetCategory</c> or <c>AssetGroup</c>: the storage yard spans two categories and four groups, while the fuel dump takes four
/// rows out of the middle of a single flat one. An Asset belongs to exactly one class and the list is flat, so a capacity refusal names one thing.
/// </para>
/// <para>
/// <b><see cref="SUNDRIES"/> is the catch-all</b> - the small, odd and awkward goods that earn no store of their own. It is deliberately not called the
/// storehouse: that word already means the tier-granted generic pool every class overflows into, and reusing it would make a capacity refusal read as one
/// thing failing twice.
/// </para>
/// <para>
/// <b><see cref="PEOPLE"/> and <see cref="INTANGIBLE"/> exist because the FK is required, not because anything stores them.</b> Population statuses are
/// counted rather than stored, and experience points and the three Force rows are not kept anywhere at all; they still need a class, so they get an
/// honest one rather than being forced into a store. Keeping the Forces out of <see cref="PEOPLE"/> leaves that class as exactly the population statuses
/// that capacity, <c>Asset.Weight</c> and population segments will all come to mean something by.
/// </para>
/// <para>
/// There is no sort order: eight fixed classes order themselves alphabetically by display name, which is the order they are declared in below.
/// </para>
/// </remarks>
internal static class StorageClassSeedData
{
    public const string ARMOURY = "Armoury";
    public const string FUEL_DUMP = "FuelDump";
    public const string GRANARY = "Granary";
    public const string INTANGIBLE = "Intangible";
    public const string LARDER = "Larder";
    public const string PEOPLE = "People";
    public const string STORAGE_YARD = "StorageYard";
    public const string SUNDRIES = "Sundries";

    /// <summary>
    /// Builds the whole storage class catalogue.
    /// </summary>
    public static IReadOnlyCollection<UpsertStorageClassDto> Build()
    {
        return
        [
            new UpsertStorageClassDto
            {
                Code = ARMOURY,
                DisplayName = "Armoury"
            },
            new UpsertStorageClassDto
            {
                Code = FUEL_DUMP,
                DisplayName = "Fuel Dump"
            },
            new UpsertStorageClassDto
            {
                Code = GRANARY,
                DisplayName = "Granary"
            },
            new UpsertStorageClassDto
            {
                Code = INTANGIBLE,
                DisplayName = "Intangible"
            },
            new UpsertStorageClassDto
            {
                Code = LARDER,
                DisplayName = "Larder"
            },
            new UpsertStorageClassDto
            {
                Code = PEOPLE,
                DisplayName = "People"
            },
            new UpsertStorageClassDto
            {
                Code = STORAGE_YARD,
                DisplayName = "Storage Yard"
            },
            new UpsertStorageClassDto
            {
                Code = SUNDRIES,
                DisplayName = "Sundries"
            }
        ];
    }
}
