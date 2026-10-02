using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Works out how much of each resource a food need takes from a tile's pool.
/// </summary>
/// <remarks>
/// <para>
/// <list type="bullet">
///   <item><description><b>Enough food in the pool:</b> the need is split across the resources in proportion to how much of each is available, as in the design doc's
///   scenario (400 Forage and 200 Hunt feeding 100 takes 66.667 and 33.333). The split is in thousandths by <see cref="ProportionalSplitter"/>, so the amounts taken sum
///   exactly to the need and no resource gives more than it has.</description></item>
///   <item><description><b>Not enough:</b> everything available is taken, and the rest of the need is the shortfall.</description></item>
/// </list>
/// </para>
/// <para>
/// Pure and database-free. Which resources are in the pool (Forage and Hunt on land, Fish on water) and what is available of each are the caller's to decide.
/// </para>
/// </remarks>
internal static class FoodExtractionCalculator
{
    /// <summary>
    /// Feeds <paramref name="need"/> from the <paramref name="available"/> resources.
    /// </summary>
    /// <param name="need">The food needed. Must not be negative.</param>
    /// <param name="available">What is available of each resource in the pool. None may be negative.</param>
    public static FoodExtraction Extract(ScaledAmount need, IReadOnlyDictionary<SustenanceResource, ScaledAmount> available)
    {
        if (need < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(need), "A food need cannot be negative.");
        }

        ScaledAmount totalAvailable = available.Values.Aggregate(ScaledAmount.Zero, (sum, amount) => sum + amount);

        // The pool cannot cover the need, so the Forces strip it bare and go short by the difference.
        if (totalAvailable <= need)
        {
            return new FoodExtraction(available.ToDictionary(entry => entry.Key, entry => entry.Value), need - totalAvailable);
        }

        // The pool can cover the need, so it is split in proportion to what each resource has.
        Dictionary<SustenanceResource, long> takenRaw = ProportionalSplitter.Split(need.RawValue, available.ToDictionary(entry => entry.Key, entry => entry.Value.RawValue));

        return new FoodExtraction(takenRaw.ToDictionary(entry => entry.Key, entry => ScaledAmount.FromRaw(entry.Value)), ScaledAmount.Zero);
    }
}
