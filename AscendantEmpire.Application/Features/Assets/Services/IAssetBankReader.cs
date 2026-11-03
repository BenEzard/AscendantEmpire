using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Assets.GetAssetBanks;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// The one place a bank's balances are read, in either of the two shapes the game asks for them.
/// </summary>
/// <remarks>
/// <para>
/// The design doc calls for a read of one bank and a read of all of them, and both go through here so that neither can drift from the other - what the
/// Settlement panel shows and what the summary ticker adds up are then the same rows assembled the same way. See <c>docs/design/asset-banks.md</c>.
/// </para>
/// <para>
/// <b>Every read hands back the Settlement and the network key with the balances.</b> A bank is one Settlement's store, and what else can draw on it is the
/// key, so a quantity without both is a figure a caller can misuse - see <see cref="AssetBankHoldings"/>.
/// </para>
/// <para>
/// Reads on the caller's context, untracked. A drawdown about to spend from the same bank must not work off these rows: it needs tracked entities, which is
/// why <see cref="IAssetBankDrawdownService"/> loads its own.
/// </para>
/// </remarks>
internal interface IAssetBankReader
{
    /// <summary>
    /// Reads every bank one Settlement can draw on: its own, and any other sharing its network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Answers not-found both when the Settlement has no bank at all and when the bank belongs to another Player, in one message: a Settlement that is not
    /// the Player's is not theirs to ask about, and saying which of the two it was would answer a question they did not get to ask.
    /// </para>
    /// <para>
    /// An unkeyed bank reaches nothing but itself. <see cref="System.Guid.Empty"/> is the absence of a key rather than a network two banks share, so it is
    /// never grouped on.
    /// </para>
    /// </remarks>
    /// <param name="playerId">The Player whose bank this must be. A Settlement of another Player's is reported as having none.</param>
    Task<ServiceResult<AssetNetworkHoldings>> GetForSettlementAsync(AscendantEmpireContext context, int playerId, int settlementId, CancellationToken cancelToken = default);

    /// <summary>
    /// Reads every bank the Player owns, with everything each one holds.
    /// </summary>
    /// <remarks>
    /// This is what a summary ticker reads. It comes back as a bank per Settlement rather than as one merged set of totals, because capacity and locality are
    /// both per Settlement: which of them can reach each other is each bank's <c>NetworkKey</c>, and adding across two keys is the caller's decision to make,
    /// knowingly.
    /// </remarks>
    Task<ServiceResult<IReadOnlyCollection<AssetBankHoldings>>> GetForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);
}
