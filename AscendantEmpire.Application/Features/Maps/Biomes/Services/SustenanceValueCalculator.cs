using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Services;

/// <summary>
/// The one formula that turns a biome's base sustenance value and a Player's summed modifiers into that Player's effective value.
/// </summary>
/// <remarks>
/// <para>
/// <c>Effective = Base + Base × ΣPercent + ΣFlat</c>, following the Biome section of <c>docs/design/campaign-map/tiles.md</c>:
/// <list type="bullet">
///   <item><description>A <c>null</c> base means the resource does not exist in the biome, and stays <c>null</c> whatever the modifiers say.</description></item>
///   <item><description>Percentages are summed by the caller and applied once, to the base only, so they add together rather than compound and never scale the flat part.</description></item>
///   <item><description>The summed percentage is floored at −100%, so no stack of penalties can take away more than the whole base.</description></item>
///   <item><description>The result is floored at 0, so flat penalties cannot leave a negative amount of food on a tile.</description></item>
/// </list>
/// </para>
/// <para>
/// Pure and database-free, so the rules are tested on their own; <see cref="IPlayerSustenanceValueReader"/> does the reading and calls this for every biome and resource.
/// </para>
/// </remarks>
internal static class SustenanceValueCalculator
{
    /// <summary>
    /// The lowest the summed percentage may go: −100%, which removes the whole base and no more.
    /// </summary>
    private static readonly ScaledPercent _MINIMUM_PERCENT_TOTAL = ScaledPercent.FromRaw(-ScaledPercent.Scale);

    /// <summary>
    /// Applies the summed modifiers to a base value.
    /// </summary>
    /// <param name="baseValue">The biome's base value, or <see langword="null"/> when the resource does not exist in the biome.</param>
    /// <param name="flatTotal">The sum of every applicable modifier's flat part. May be negative.</param>
    /// <param name="percentTotal">The sum of every applicable modifier's percentage part. May be negative; floored at −100%.</param>
    /// <returns>The effective value, never negative, or <see langword="null"/> when <paramref name="baseValue"/> is.</returns>
    public static ScaledAmount? CalculateEffectiveValue(ScaledAmount? baseValue, ScaledAmount flatTotal, ScaledPercent percentTotal)
    {
        // A resource the biome does not have can never be given one.
        if (baseValue is null)
        {
            return null;
        }

        ScaledPercent flooredPercentTotal = percentTotal.RawValue < _MINIMUM_PERCENT_TOTAL.RawValue ? _MINIMUM_PERCENT_TOTAL : percentTotal;

        // The percentage scales the base alone; the flat part is added afterwards so it is never scaled.
        ScaledAmount effectiveValue = baseValue.Value.AddBonus(flooredPercentTotal) + flatTotal;

        return effectiveValue < ScaledAmount.Zero ? ScaledAmount.Zero : effectiveValue;
    }

    /// <summary>
    /// What is left of a Player's effective value on a tile once the tile's Player-neutral <c>Taken</c> amount is removed: <c>MAX(0, Effective − Taken)</c>.
    /// </summary>
    /// <remarks>
    /// Floored at 0 because <c>Taken</c> can exceed a weaker Player's effective value: a stronger Player may have stripped the tile past what the weaker Player could ever reach.
    /// See "What a tile stores: the amount taken" in <c>docs/design/campaign-map/depletion.md</c>.
    /// </remarks>
    /// <param name="effectiveValue">The Player's effective value, from <see cref="CalculateEffectiveValue"/>, or <see langword="null"/> when the resource does not exist in the biome.</param>
    /// <param name="taken">How much has been taken from the tile, by anyone.</param>
    /// <returns>The amount available to the Player, never negative, or <see langword="null"/> when <paramref name="effectiveValue"/> is.</returns>
    public static ScaledAmount? CalculateAvailableValue(ScaledAmount? effectiveValue, ScaledAmount taken)
    {
        // A resource the biome does not have is not "none left": it stays absent, so the caller can tell the two apart.
        if (effectiveValue is null)
        {
            return null;
        }

        ScaledAmount availableValue = effectiveValue.Value - taken;

        return availableValue < ScaledAmount.Zero ? ScaledAmount.Zero : availableValue;
    }
}
