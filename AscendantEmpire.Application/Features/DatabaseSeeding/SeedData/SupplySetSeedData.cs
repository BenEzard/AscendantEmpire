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
/// <para>
/// Human food is the edible part of the Foodstuffs meta-category. Poisons sit there by catalogue placement rather than by use, and Wild Herbs are the
/// medical set's member, so neither is a ration. A foodstuff added to the catalogue later joins the set by being named here.
/// </para>
/// <para>
/// Fodder is deliberately <see cref="SupplyType.Fuel"/>. A horse burns it the way an engine burns diesel, while the rider still eats human food, so a
/// mounted body carries a requirement against both sets rather than one blended ration.
/// </para>
/// <para>
/// There are no medical Assets in the catalogue beyond Wild Herbs, so the repair set holds it alone. It is a real set rather than a placeholder: a repair
/// draw with one member always draws that member, and adding the rest later only widens the draw.
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

    /// <summary>
    /// What one troop's food costs in Berries. They are thin rations, which is the rate <c>docs/design/campaign-map/food-yield.md</c> fixes.
    /// </summary>
    private const long _BERRIES_PER_TROOP = 1_670;

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
                    Member("Foodstuffs.Meat.Game"),
                    Member("Foodstuffs.Meat.Birds"),
                    Member("Foodstuffs.Meat.Fish"),
                    Member("Foodstuffs.Meat.Shellfish"),
                    Member("Foodstuffs.Meat.Insects"),

                    Member("Foodstuffs.PlantFoods.Fruit"),
                    Member("Foodstuffs.PlantFoods.Berries", _BERRIES_PER_TROOP),
                    Member("Foodstuffs.PlantFoods.Roots"),
                    Member("Foodstuffs.PlantFoods.Tubers"),
                    Member("Foodstuffs.PlantFoods.Greens"),
                    Member("Foodstuffs.PlantFoods.Mushrooms"),
                    Member("Foodstuffs.PlantFoods.Bulbs"),

                    Member("Foodstuffs.Staples.WildGrains"),
                    Member("Foodstuffs.Staples.Seeds"),
                    Member("Foodstuffs.Staples.Legumes"),
                    Member("Foodstuffs.Staples.Nuts"),

                    Member("Foodstuffs.SpecialFoods.Honey"),
                    Member("Foodstuffs.SpecialFoods.Seaweed"),
                    Member("Foodstuffs.SpecialFoods.ChilliPeppers")
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
    private static UpsertSupplySetAssetDto Member(string assetCode, long amountPerUnit = DEFAULT_AMOUNT_PER_UNIT, int selectionWeight = 1)
    {
        return new UpsertSupplySetAssetDto
        {
            AssetCode = assetCode,
            AmountPerUnit = amountPerUnit,
            SelectionWeight = selectionWeight
        };
    }
}
