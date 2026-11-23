using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Settlements.Classes;

/// <summary>
/// How much of one resource a Settlement takes off one of its territory tiles in a Round.
/// </summary>
/// <remarks>
/// <para>
/// The two kinds of yield are different arithmetic because they are different things. A <b>sustenance</b> value depletes and regrows, so the take is capped
/// at the regrowth and no more: the tile ends the Round as full as it started and a Force still finds its standing stock. A <b>material</b> value is static
/// and is never depleted, so the take is the biome's base value scaled by the owner's extraction efficiency, every Round, indefinitely. See "Harvest" in <c>docs/design/settlements.md</c>.
/// </para>
/// <para>
/// Pure and database-free, so the rules are tested on their own; the start-of-Round process does the reading and the banking and calls this per tile and
/// resource.
/// </para>
/// </remarks>
internal static class SettlementHarvestCalculator
{
    /// <summary>
    /// Whole units of one resource one civilian takes from one tile in a Round.
    /// </summary>
    /// <remarks>
    /// <b>A ceiling, not an economy dial.</b> On any terrain worth settling the regrowth or the base value is the smaller of the three figures below, so this
    /// rarely binds and tuning it would not move what a Settlement earns. It becomes the real constraint once civilians are allocated to tiles rather than
    /// every one of them working all of them - see <see cref="SettlementWorkAllocation"/>.
    /// </remarks>
    public const int CIVILIAN_HARVEST_RATE = 1;

    /// <summary>
    /// The share of each material's base value a Player's Settlements extract before any technology improves it.
    /// </summary>
    /// <remarks>
    /// The starting efficiency the Player's extraction efficiency modifiers apply to. Low on purpose, so materials barely trickle in until <c>PrimitiveCrafting1</c>
    /// grants the points that lift each one to full. The biome values stay what the land holds; this is how little of it a people without tools get out.
    /// </remarks>
    public static readonly IReadOnlyDictionary<MaterialResource, ScaledPercent> STARTING_MATERIAL_EFFICIENCY = new Dictionary<MaterialResource, ScaledPercent>
    {
        [MaterialResource.Wood] = ScaledPercent.FromWholePercent(5),
        [MaterialResource.Stone] = ScaledPercent.FromWholePercent(5),
        [MaterialResource.Bone] = ScaledPercent.FromWholePercent(5)
    };

    /// <summary>
    /// What a Settlement takes of one sustenance resource: the least of what is left to its owner, what the tile regrows, and what its civilians can work.
    /// </summary>
    /// <param name="available">What the owner can still reach on the tile, or <see langword="null"/> where the biome does not have the resource.</param>
    /// <param name="regrowth">What the tile gives back at the end of the Round, or <see langword="null"/> where the biome states no Regeneration Percent.</param>
    /// <param name="civilians">The civilians working the tile, from <see cref="SettlementWorkAllocation.CiviliansOnTile"/>.</param>
    /// <returns>The amount to take, which is never negative.</returns>
    public static ScaledAmount CalculateSustenanceTake(ScaledAmount? available, ScaledAmount? regrowth, long civilians)
    {
        // A resource the biome does not have is never harvested, and neither is one with no regrowth: without a Regeneration Percent there is no cap to
        // harvest under, and taking the whole standing stock instead is the one thing the cap exists to prevent.
        if (available is null || regrowth is null)
        {
            return ScaledAmount.Zero;
        }

        return Smallest(available.Value, regrowth.Value, StaffedShare(civilians));
    }

    /// <summary>
    /// What a Settlement takes of one material resource: the biome's base value scaled by the owner's extraction efficiency, unless its civilians cannot work that much.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Efficiency scales what the tile yields <b>before</b> the civilian cap, so a small Settlement on a rich seam is limited by its hands and not by both at once.
    /// </para>
    /// <para>
    /// Nothing is subtracted for what has been taken before, because nothing records it: a material value is static and the same amount is there next Round.
    /// </para>
    /// </remarks>
    /// <param name="baseValue">The biome's base value for the material, or <see langword="null"/> where the biome does not have it.</param>
    /// <param name="efficiency">The owner's extraction efficiency for the material, from <c>PlayerExtractionEfficiencies</c>.</param>
    /// <param name="civilians">The civilians working the tile, from <see cref="SettlementWorkAllocation.CiviliansOnTile"/>.</param>
    /// <returns>The amount to take, which is never negative.</returns>
    public static ScaledAmount CalculateMaterialTake(ScaledAmount? baseValue, ScaledPercent efficiency, long civilians)
    {
        if (baseValue is null)
        {
            return ScaledAmount.Zero;
        }

        ScaledAmount yield = baseValue.Value.CalculateBonus(efficiency);
        ScaledAmount staffedShare = StaffedShare(civilians);
        ScaledAmount take = yield < staffedShare ? yield : staffedShare;

        return take < ScaledAmount.Zero ? ScaledAmount.Zero : take;
    }

    /// <summary>
    /// The most the civilians working a tile can take of one resource from it.
    /// </summary>
    private static ScaledAmount StaffedShare(long civilians)
    {
        return civilians <= 0 ? ScaledAmount.Zero : ScaledAmount.FromWhole(civilians * CIVILIAN_HARVEST_RATE);
    }

    /// <summary>
    /// The smallest of the three caps, floored at zero so a stripped tile never yields a negative amount.
    /// </summary>
    private static ScaledAmount Smallest(ScaledAmount available, ScaledAmount regrowth, ScaledAmount staffedShare)
    {
        ScaledAmount smallest = available;

        if (regrowth < smallest)
        {
            smallest = regrowth;
        }

        if (staffedShare < smallest)
        {
            smallest = staffedShare;
        }

        return smallest < ScaledAmount.Zero ? ScaledAmount.Zero : smallest;
    }
}
