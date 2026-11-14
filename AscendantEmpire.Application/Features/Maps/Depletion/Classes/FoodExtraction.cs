using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// What feeding a need from a tile's food sources took, and how much of the need went unmet.
/// </summary>
/// <param name="Taken">
/// How much of each source's Asset was taken, in Asset units. Every source offered appears, with 0 where nothing was taken from it.
/// </param>
/// <param name="NeedMet">The part of the need the tile covered, in need units - individuals fed for the Turn.</param>
/// <param name="Shortfall">The part of the need the tile could not meet, in need units; 0 when it was met in full.</param>
/// <remarks>
/// <see cref="Taken"/> and the other two are in different measures on purpose: what a tile loses is an amount of an Asset, while what a Force gets is a
/// number of rations, and the two only coincide where every Asset happens to cost 1.0.
/// </remarks>
internal sealed record FoodExtraction(IReadOnlyDictionary<FoodSourceKey, ScaledAmount> Taken, ScaledAmount NeedMet, ScaledAmount Shortfall);
