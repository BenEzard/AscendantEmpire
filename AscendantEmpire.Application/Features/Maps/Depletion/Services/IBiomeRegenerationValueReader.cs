using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Reads every biome's Regeneration Percent and base sustenance values: what an unclaimed tile regrows against at the end of a Round.
/// </summary>
/// <remarks>
/// <para>
/// The one read of those values, shared by the end-of-Round regrowth (<c>RegenerateTileSustenance</c>) and the support forecast (<see cref="TileSupportForecaster"/>), so
/// the forecast can never regrow a tile by a different amount from the Round's end.
/// </para>
/// <para>
/// Reads through the caller's context.
/// </para>
/// </remarks>
internal interface IBiomeRegenerationValueReader
{
    /// <summary>
    /// Every biome's regeneration values, keyed by biome.
    /// </summary>
    /// <remarks>
    /// The biome catalogue is small, so it is read whole rather than narrowed to the biomes a caller happens to need.
    /// </remarks>
    Task<Dictionary<BiomeType, BiomeRegenerationValues>> GetValuesAsync(AscendantEmpireContext context, CancellationToken cancelToken = default);
}
