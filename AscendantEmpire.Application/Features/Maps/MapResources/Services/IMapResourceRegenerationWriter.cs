using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Writes a Round's regeneration of a Game's renewable MapResources.
/// </summary>
/// <remarks>
/// Writes through the caller's context, joining its transaction if it has one open.
/// </remarks>
internal interface IMapResourceRegenerationWriter
{
    /// <summary>
    /// Writes the plan in one transaction: the deletes, the updates, the instances spread created, and the discoveries they inherit from their spreaders.
    /// </summary>
    /// <remarks>
    /// The pipeline is not transactional, so writing spread, growth and regrowth together is what keeps a failure from leaving the Round half regenerated.
    /// </remarks>
    Task WriteAsync(AscendantEmpireContext context, int gameId, int currentRoundNumber, MapResourceRegenerationPlan plan, CancellationToken cancelToken = default);
}
