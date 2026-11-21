using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The supply sets a new database starts with — what a force eats, what its mounts burn, and what its wounded are patched up with.
/// </summary>
/// <remarks>
/// <para>
/// Every set is enumerated here, membership and rates together, so what the seed writes is readable in one place and is the same shape the Supply Sets
/// dialog saves. A rate belongs to the member that states it rather than to the Asset, so the same Asset may cost one thing in one set and something else
/// in another; a member that states no rate costs <see cref="DEFAULT_AMOUNT_PER_UNIT"/>.
/// </para>
/// </remarks>
internal static class SupplySetSeedData
{
    public const string HUMAN_FOOD = "human-food";
    public const string MOUNT_FODDER = "mount-fodder";
    public const string HUMAN_MEDICAL = "human-medical";

    /// <summary>
    /// What one unit of a need costs in a member that states no rate of its own — 1.0, scaled.
    /// </summary>
    public const long DEFAULT_AMOUNT_PER_UNIT = 1_000;

    private const string _FODDER = "RawMaterials.RawMaterials.Fodder";
    private const string _WILD_HERBS = "Foodstuffs.SpecialFoods.WildHerbs";

    /// <summary>
    /// The starting sets, each with the Assets that satisfy it and what one unit of the need costs in each.
    /// </summary>
    public static IReadOnlyCollection<UpsertSupplySetDto> BuildSets()
    {
        return
        [
            new UpsertSupplySetDto
            {
                Code = HUMAN_FOOD,
                DisplayName = "Human Food",
                SupplyType = SupplyType.Food,
                Assets =
                [
                    Member(assetCode: "Foodstuffs.Meat.Hunt"),
                    Member(assetCode: "Foodstuffs.Meat.Fish"),
                    Member(assetCode: "Foodstuffs.Meat.Shellfish"),

                    Member(assetCode: "Foodstuffs.PlantFoods.Forage"),
                    Member(assetCode: "Foodstuffs.PlantFoods.Berries"),
                    Member(assetCode: "Foodstuffs.PlantFoods.Mushrooms"),

                    Member(assetCode: "Foodstuffs.Staples.WildGrains"),
                    Member(assetCode: "Foodstuffs.Staples.Nuts"),

                    Member(assetCode: "Foodstuffs.SpecialFoods.Honey"),
                    Member(assetCode: "Foodstuffs.SpecialFoods.ChilliPeppers")
                ]
            },
            new UpsertSupplySetDto
            {
                Code = MOUNT_FODDER,
                DisplayName = "Mount Fodder",
                SupplyType = SupplyType.Fuel,
                Assets = [Member(_FODDER)]
            },
            new UpsertSupplySetDto
            {
                Code = HUMAN_MEDICAL,
                DisplayName = "Human Medical Supplies",
                SupplyType = SupplyType.Repair,
                Assets = [Member(_WILD_HERBS)]
            }
        ];
    }

    /// <summary>
    /// One member of one set. Weight is read only for a repair draw, where equal weights mean equal odds.
    /// </summary>
    /// <param name="randomWeighting">How heavily this Asset is weighted when one member is drawn at random. Read only for a Repair set.</param>
    private static UpsertSupplySetAssetDto Member(string assetCode, long amountPerUnit = DEFAULT_AMOUNT_PER_UNIT, int randomWeighting = 1)
    {
        return new UpsertSupplySetAssetDto
        {
            AssetCode = assetCode,
            AmountPerUnit = amountPerUnit,
            RandomWeighting = randomWeighting
        };
    }
}
