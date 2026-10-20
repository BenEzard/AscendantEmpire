using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Resolves a Game's territory through <see cref="TileTerritory"/> and writes what changed.
/// </summary>
/// <remarks>
/// <para>
/// The same seam <see cref="IFieldOfViewService"/> is for vision: the pure rules live below, the data loading and the writes live here, and two entry points let a caller
/// either have its own context or stage the work on theirs. Both going through here is what stops the end-of-Round pass and anything else that grants ground drifting
/// apart.
/// </para>
/// <para>
/// <see cref="ApplyAsync"/> is the <b>single writer</b> of the ownership columns and the claim junction. "Ownership trumps claim" is spent once, in
/// <see cref="TerritoryWorkingSet.AssertOwnership"/>, and the write is spent once, here.
/// </para>
/// </remarks>
internal interface ITileTerritoryService
{
    /// <summary>
    /// Resolves and writes one Game's territory, on a context of its own.
    /// </summary>
    /// <remarks>Saves what it decided, because it owns the context it opened.</remarks>
    Task<ServiceResult<TerritoryChanges>> ResolveGameTerritoryAsync(int gameId, CancellationToken cancelToken = default);

    /// <summary>
    /// The same, staged on the caller's context so it is saved with whatever else they are writing.
    /// </summary>
    /// <remarks>Does not call <c>SaveChangesAsync</c>: a context this service did not open is not its to save.</remarks>
    Task<ServiceResult<TerritoryChanges>> ResolveGameTerritoryAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages a set of changes the caller worked out for itself, without resolving anything.
    /// </summary>
    /// <remarks>
    /// For a caller that already knows its own small delta - a Structure asserting over its ground and its Settlement's radius - so the writes still go through one
    /// place. Does not save, for the same reason as above.
    /// </remarks>
    Task<ServiceResultBase> ApplyAsync(AscendantEmpireContext context, int gameId, TerritoryChanges changes, CancellationToken cancelToken = default);
}
