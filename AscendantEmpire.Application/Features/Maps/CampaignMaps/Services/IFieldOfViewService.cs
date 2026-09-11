using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Works out what Observers can see right now, loading the terrain the sight rules need and applying them.
/// </summary>
/// <remarks>
/// <para>
/// The forwards direction of vision — "what can these Observers see" — used by the live map read, and by <see cref="IWitnessService"/> once it has found
/// the Observers that matter. Both going through here, and through <see cref="FieldOfView"/> beneath it, is what keeps live vision and intelligence from
/// drifting apart.
/// </para>
/// <para>
/// Side-effect free. Exploring the map by looking at it — stamping <c>FirstSeenOnTurnId</c> — is the map read's business, not this service's.
/// </para>
/// </remarks>
internal interface IFieldOfViewService
{
    /// <summary>
    /// What every Observer one Player owns can see: their standing Structures, then their Units.
    /// </summary>
    /// <returns>One entry per Observer that sees anything, Structures first. An Observer with a negative Sight is not an Observer and is left out.</returns>
    Task<ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>> GetPlayerFieldOfViewAsync(int gameId, int playerId, CampaignMapGeometry geometry, CancellationToken cancelToken = default);

    /// <summary>
    /// What the given Observers can see, reading terrain through the caller's context.
    /// </summary>
    /// <remarks>
    /// Terrain is read in two passes — the Observers' own tiles, which decide each range, then everything within those ranges — so a blinded Observer never
    /// pays for the square it cannot see.
    /// </remarks>
    /// <returns>
    /// One entry per Observer that sees anything, in the order given. An Observer with a negative Sight, or standing on a tile that belongs to no tile of
    /// this Game, is left out.
    /// </returns>
    Task<ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>> GetFieldOfViewAsync(
        AscendantEmpireContext context,
        int gameId,
        CampaignMapGeometry geometry,
        IReadOnlyCollection<SightObserver> observers,
        CancellationToken cancelToken = default);
}
