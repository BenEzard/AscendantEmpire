using AscendantEmpire.Application.Helpers.ScaledNumerics;

using System.Numerics;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Works out a Player's extraction efficiency for one Asset from a starting figure and their summed modifiers.
/// </summary>
/// <remarks>
/// <c>Start + Start × ΣPercent + ΣFlat</c>, floored at 0 - the formula every Player modifier family uses, with the starting efficiency in the place of the base.
/// The start belongs to the extraction path calling this, not to the modifiers, so the same rows can serve paths that begin from different figures.
/// </remarks>
internal static class ExtractionEfficiencyCalculator
{
    /// <summary>
    /// Applies the summed modifiers to a starting efficiency.
    /// </summary>
    /// <param name="flatTotal">The sum of every applicable modifier's flat part, in percentage points. May be negative.</param>
    /// <param name="percentTotal">The sum of every applicable modifier's percentage part, as a share of the start. May be negative.</param>
    /// <returns>The efficiency, never negative.</returns>
    public static ScaledPercent CalculateEfficiency(ScaledPercent startingEfficiency, ScaledPercent flatTotal, ScaledPercent percentTotal)
    {
        BigInteger percentOfStart = (BigInteger)startingEfficiency.RawValue * percentTotal.RawValue / ScaledPercent.Scale;

        BigInteger efficiency = startingEfficiency.RawValue + percentOfStart + flatTotal.RawValue;

        return efficiency <= 0 ? ScaledPercent.Zero : ScaledPercent.FromRaw((long)efficiency);
    }
}
