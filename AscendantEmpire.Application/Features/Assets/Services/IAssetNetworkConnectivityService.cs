using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Domain.Entities.Assets;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// The one writer of <see cref="AssetBank.NetworkKey"/>, which is what says whose banks may draw on each other.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing else may write the column.</b> It is derived data with no referential integrity, so a second writer means the wrong Settlements can reach each
/// other and nothing in the schema catches it. <see cref="FindFaultsForPlayerAsync"/> exists because that is a real risk rather than a theoretical one.
/// </para>
/// <para>
/// <b>Every bank is its own network of one today.</b> Nothing in the codebase connects two Settlements: there is no road anywhere - no Structure, no seed
/// row, no tile feature - and the river and coastline walks are their own work. This is the seam those three fill, so that each one changes what connectivity
/// means in a single place. See CM-64 (roads), RS-48 (rivers) and RS-49 (coastline), and <c>docs/design/asset-banks.md</c> for the rules they build to.
/// </para>
/// <para>
/// <b>Balances never move.</b> Connecting and disconnecting Settlements are re-keys, so no balance row is touched and no bank is created or deleted. The one
/// sanctioned exception anywhere in the model is the camp feeder push (RS-35), which is not a re-key at all.
/// </para>
/// </remarks>
internal interface IAssetNetworkConnectivityService
{
    /// <summary>
    /// Gives a freshly staged bank the network it is born into.
    /// </summary>
    /// <remarks>
    /// Synchronous, and takes the entity rather than an id, because the bank a founding stages has not been saved: a database recompute could not see it, and
    /// saving first so that one could would let a Settlement be committed with no key at all. Provisioning therefore calls this instead of writing the column
    /// itself, which is what keeps the single-writer rule true at founding as well as at a re-key.
    /// </remarks>
    void AssignKeyToNewBank(AssetBank bank);

    /// <summary>
    /// Isolates a bank that has just changed hands, so the network it sits in is the conqueror's rather than the one it was taken out of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The key a conquest leaves behind is the one thing a recompute cannot catch.</b> Connectivity is assessed per Player, so where the losing Player held two connected Settlements and only
    /// one was taken, the captured bank keeps a key the loser's remaining bank still carries - and <see cref="FindFaultsForPlayerAsync"/> walks one Player at a time, so neither side ever reports
    /// it. Two empires would draw on each other with nothing in the schema or in the check to say so.
    /// </para>
    /// <para>
    /// <b>A network of one, not the conqueror's network.</b> Which of the conqueror's banks the captured one can actually reach is <see cref="RecomputeForPlayerAsync"/>'s to decide, and a
    /// conquest calls it straight afterwards. Choosing here instead would make the conquest a second writer of connectivity policy, which is the thing this interface exists to prevent.
    /// </para>
    /// <para>Synchronous, and takes the entity, for the same reason as <see cref="AssignKeyToNewBank"/>: the caller is mid-change, and its save is what commits the new key.</para>
    /// </remarks>
    void AssignKeyToCapturedBank(AssetBank bank);

    /// <summary>
    /// Re-keys one Player's banks to match what their Settlements can currently reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Works on the caller's context and does not save</b>, so the re-keys it stages commit with whatever else the caller is doing.
    /// </para>
    /// <para>
    /// <b>An ownership move has to be saved before this runs.</b> Which banks belong to the Player is read from the database rather than from the change
    /// tracker, so a conquest that sets <c>AssetBank.PlayerId</c> and recomputes in the same unit of work would assess both players against the ownership they
    /// no longer have, and leave both with a stale key. <see cref="IAssetBankConquestService"/> saves the hand-over first, then recomputes for each side.
    /// </para>
    /// <para>
    /// <b>Idempotent.</b> A second run changes nothing, which is what lets a caller recompute both sides of a conquest without ordering the two calls - see
    /// <see cref="IAssetBankConquestService"/>.
    /// </para>
    /// </remarks>
    /// <param name="playerId">The Player whose banks to re-key. Another Player's are never touched, since connectivity is assessed per Player.</param>
    Task<ServiceResultBase> RecomputeForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);

    /// <summary>
    /// Reports the banks whose key disagrees with what <see cref="RecomputeForPlayerAsync"/> would give them, without writing anything.
    /// </summary>
    /// <remarks>
    /// The mitigation for a derived column with no constraint behind it. A fault here means some other writer has touched the column, or a re-key was never
    /// run after an event that needed one.
    /// </remarks>
    Task<ServiceResult<IReadOnlyCollection<AssetNetworkFault>>> FindFaultsForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);
}
