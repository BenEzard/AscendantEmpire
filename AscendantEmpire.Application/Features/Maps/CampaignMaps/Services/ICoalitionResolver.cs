using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Decides which Players in a Game share vision, and so fold their intelligence together.
/// </summary>
/// <remarks>
/// <para>
/// The seam the design calls for rather than the model: there is no foreign relations model in the codebase yet, so today every active Player is a
/// coalition of one (<see cref="SoloCoalitionResolver"/>). F-27 swaps the implementation for one that consults diplomatic arrangements, and nothing that
/// folds intelligence has to change. See "Coalitions" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// Resolved fresh each time it is asked, never stored. A report written under one arrangement is a historical fact and stays as it was when the
/// arrangement changes.
/// </para>
/// </remarks>
internal interface ICoalitionResolver
{
    /// <summary>
    /// The coalitions of a Game's active Players, which are disjoint and together cover every one of them.
    /// </summary>
    Task<IReadOnlyCollection<VisionCoalition>> ResolveAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);
}
