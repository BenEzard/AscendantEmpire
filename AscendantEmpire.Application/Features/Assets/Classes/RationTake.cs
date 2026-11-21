using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// What levelling a Force's carried food against a need came to: how much of each Asset to take, and how many rations that is worth.
/// </summary>
/// <param name="TakeRawByAssetId">How much of each Asset to take, raw fixed-point. An Asset nothing was taken from is left out rather than carried as a zero.</param>
/// <param name="Fed">
/// The part of the need the load covers, in rations. Never more than the need, and never more than the take is worth.
/// </param>
/// <remarks>
/// The two are in different measures on purpose, the same way <c>FoodExtraction</c>'s are: what a bank loses is an amount of an Asset, while what a Force gets
/// is a number of rations.
/// </remarks>
internal sealed record RationTake(IReadOnlyDictionary<int, long> TakeRawByAssetId, ScaledAmount Fed)
{
    /// <summary>
    /// Nothing taken and nobody fed, for a Force carrying nothing its design can eat.
    /// </summary>
    public static RationTake None { get; } = new(new Dictionary<int, long>(), ScaledAmount.Zero);
}
