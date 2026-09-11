using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Answers the one question intelligence rests on: which coalitions could see tile T at sequence N of a Turn, and at what fidelity.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inverted.</b> Rather than computing every other Player's whole field of view and testing a path against it — one whole-map pass per Player per Turn,
/// to answer a question about a handful of tiles — it searches a box round each probed tile for Observers close enough to have seen it, and runs the
/// sight rules over only those. The cost scales with the length of the path and how crowded the ground around it is, not with the size of the map or
/// the number of Players.
/// </para>
/// <para>
/// The box is a square of <see cref="ISightDataReader.GetMaxSightReachAsync"/> round each probed tile: nothing further away than the furthest anybody
/// could see can have seen it. Within it, each Observer's view is computed once, by <see cref="IFieldOfViewService"/> — the same code the live map read
/// uses — and reused for every probe.
/// </para>
/// <para>
/// <b>Evaluated as at the probed sequence.</b> Every other Player's Observers are frozen during a Turn, except that the acting Player can destroy them.
/// An Observer destroyed during the Turn counts for every sequence before its destruction, so a watchtower that saw the cavalry and was then razed still
/// files its report.
/// </para>
/// <para>
/// Side-effect free, and with no production caller until the end-of-turn intelligence fold (F-24). See
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// </remarks>
internal interface IWitnessService
{
    /// <summary>
    /// Answers every probe in the query, reading through the caller's context so it sees the Turn's uncommitted history.
    /// </summary>
    /// <returns>
    /// An entry for every probe key, empty when no observing coalition saw it. Fails without reading anything when two probes share a key; not found when
    /// the Game does not exist.
    /// </returns>
    Task<ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>> FindWitnessesAsync(
        AscendantEmpireContext context,
        WitnessQuery query,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Answers every probe in the query on a context of its own, for callers not already inside a transaction.
    /// </summary>
    /// <inheritdoc cref="FindWitnessesAsync(AscendantEmpireContext, WitnessQuery, CancellationToken)" path="/returns"/>
    Task<ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>> FindWitnessesAsync(WitnessQuery query, CancellationToken cancelToken = default);
}
