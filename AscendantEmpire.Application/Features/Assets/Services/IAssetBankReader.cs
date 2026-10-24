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
/// <b>Every read hands back the memberships with the balances.</b> A bank is only spendable by the Settlements that draw on it, so a quantity without them
/// is a figure a caller can misuse - see <see cref="AssetBankHoldings"/>.
/// </para>
/// <para>
/// Reads on the caller's context, untracked. A drawdown about to spend from the same bank must not work off these rows: it needs tracked entities, which is
/// why <see cref="IAssetBankDrawdownService"/> loads its own.
/// </para>
/// </remarks>
internal interface IAssetBankReader
{
    /// <summary>
    /// Reads the bank one Settlement draws on, with everything it holds.
    /// </summary>
    /// <remarks>
    /// Answers not-found both when the Settlement has no membership at all and when the bank it points at belongs to another Player, in one message: a
    /// Settlement that is not the Player's is not theirs to ask about, and saying which of the two it was would answer a question they did not get to ask.
    /// </remarks>
    /// <param name="playerId">The Player whose bank this must be. A Settlement of another Player's is reported as having none.</param>
    Task<ServiceResult<AssetBankHoldings>> GetForSettlementAsync(AscendantEmpireContext context, int playerId, int settlementId, CancellationToken cancelToken = default);

    /// <summary>
    /// Reads every bank the Player owns, with everything each one holds.
    /// </summary>
    /// <remarks>
    /// This is what a summary ticker reads. It comes back as a bank per pool rather than as one merged set of totals, because two banks exist precisely
    /// because their Settlements cannot reach each other: adding them up is the caller's decision to make, knowingly.
    /// </remarks>
    Task<ServiceResult<IReadOnlyCollection<AssetBankHoldings>>> GetForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);
}
