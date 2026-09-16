using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// What feeding a need from a tile's pool of resources took, and how much of the need went unmet.
/// </summary>
/// <param name="Taken">The amount taken from each resource in the pool. Every resource offered appears, with 0 where nothing was taken from it.</param>
/// <param name="Shortfall">The part of the need the pool could not meet; 0 when it was met in full.</param>
internal sealed record FoodExtraction(IReadOnlyDictionary<SustenanceResource, ScaledAmount> Taken, ScaledAmount Shortfall)
{
    /// <summary>
    /// The total taken across every resource: the part of the need that was met.
    /// </summary>
    public ScaledAmount TotalTaken => Taken.Values.Aggregate(ScaledAmount.Zero, (sum, amount) => sum + amount);
}
