using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// One Player's summed extraction efficiency modifiers, per Asset, ready to apply to whichever starting efficiency an extraction path supplies.
/// </summary>
internal sealed class PlayerExtractionEfficiencies(IReadOnlyDictionary<int, (long FlatRawTotal, long PercentRawTotal)> totalsByAssetId)
{
    /// <summary>
    /// The Player's efficiency for an Asset, starting from <paramref name="startingEfficiency"/>; just the start when they hold no modifier for it.
    /// </summary>
    public ScaledPercent GetEfficiency(int assetId, ScaledPercent startingEfficiency)
    {
        (long flatRawTotal, long percentRawTotal) = totalsByAssetId.GetValueOrDefault(assetId);

        return ExtractionEfficiencyCalculator.CalculateEfficiency(startingEfficiency, ScaledPercent.FromRaw(flatRawTotal), ScaledPercent.FromRaw(percentRawTotal));
    }
}
