using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Structures;

namespace AscendantEmpire.Application.Features.Structures.Services;

/// <summary>
/// The single writer for a campaign map Structure's life - founding, construction, capture and razing - recording each change in <see cref="StructureHistory"/> together with the change itself.
/// </summary>
/// <remarks>
/// <para>
/// The Structure sibling of <c>IForceLifecycleWriter</c>. Every path that changes a Structure goes through here, so that "a Structure was razed" has exactly one place it can be observed. Appending
/// history at each call site instead would eventually miss one, and a missed path fails <b>silently</b>: no error, just intelligence that never arrives. See
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// The rule every method keeps is <b>no record, no change</b>. If the Turn's sequence number cannot be allocated, nothing is staged or written and the caller gets a failure.
/// </para>
/// <para>
/// <b>Nothing calls this yet.</b> No part of play founds, builds, captures or razes a Structure today. Each of these must go through it when it is built:
/// <list type="bullet">
///   <item><description>the settle order that founds a village or camp (<c>force-instances-roadmap.md</c>, Phase 10) - <see cref="FoundAsync"/>;</description></item>
///   <item><description>construction in the <c>BuildingStructure</c> end-of-turn process - <see cref="ProgressAsync"/>;</description></item>
///   <item><description>siege and capture - <see cref="CaptureAsync"/> and <see cref="RazeAsync"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// Campaign map Structures only: every method refuses one with a <c>BattleMapTileId</c>, since nothing on a battle map is visible to a campaign map observer. Works through the caller's context,
/// so it joins whatever transaction they already have open.
/// </para>
/// </remarks>
public interface IStructureLifecycleWriter
{
    /// <summary>
    /// Adds a new Structure to the context and saves it, together with the <c>Founded</c> row that records it when there is a Turn to stamp it with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Saves, unlike the other methods,</b> for the same reason as <c>IForceLifecycleWriter.CreateAsync</c>: the row has to carry the new Structure's id, which it does not have until it is
    /// inserted. The Structure is saved first and its rows second, inside a transaction this method opens when the caller has none. Anything else the caller has pending is flushed with the first
    /// save, and on failure the context should be discarded.
    /// </para>
    /// <para>
    /// A Structure founded already fully built gets a <c>Founded</c> row and then a <c>Completed</c> row, on consecutive sequences, so that "it was finished" always comes from a <c>Completed</c> row
    /// whichever way it happened.
    /// </para>
    /// <para>
    /// A <see langword="null"/> <paramref name="playerTurnId"/> places the Structure with no rows and a <see cref="StructureInstance.CreatedOnTurnId"/> of 0: nobody could have watched it appear.
    /// </para>
    /// </remarks>
    /// <param name="structure">A new, unsaved campaign map Structure, standing, with a <see cref="StructureInstance.CompletionPercent"/> from 0 to 100.</param>
    /// <param name="playerTurnId">The open Turn to record the founding against, or <see langword="null"/> for a placement outside any Turn.</param>
    Task<ServiceResultBase> FoundAsync(AscendantEmpireContext context, StructureInstance structure, int? playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages construction advancing on a Structure, and the row that records it, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Does not save.</b> On failure nothing has been staged, and the caller should refuse the change rather than save.
    /// </para>
    /// <para>
    /// A step that reaches 100% - anything above is clamped to it - writes a <c>Completed</c> row and stamps <see cref="StructureInstance.CompletedOnTurnId"/>; any other writes a
    /// <c>Progressed</c> row. A step that does not advance, or one on a Structure that is already finished or razed, is refused.
    /// </para>
    /// </remarks>
    /// <param name="completionPercent">The progress to set, which must be more than the Structure has now.</param>
    Task<ServiceResultBase> ProgressAsync(AscendantEmpireContext context, StructureInstance structure, double completionPercent, int playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages a Structure changing hands, and the <c>Captured</c> row that records who held it before, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <b>Does not save.</b> A capture by the Player who already holds it, or of a razed Structure, is refused.
    /// </remarks>
    Task<ServiceResultBase> CaptureAsync(AscendantEmpireContext context, StructureInstance structure, int newOwnerPlayerId, int playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages a Structure's razing, and the <c>Razed</c> row that records it, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <b>Does not save.</b> Razing is a soft delete: it stamps <see cref="StructureInstance.RemovedOnTurnId"/> and the row stays, which frees the tile's slot for a new Structure while every
    /// history, report and known-structure row keeps pointing at this one. A Structure that is already razed is refused.
    /// </remarks>
    Task<ServiceResultBase> RazeAsync(AscendantEmpireContext context, StructureInstance structure, int playerTurnId, CancellationToken cancelToken = default);
}
