using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

public interface ICampaignTurnService
{
    /// <summary>
    /// Returns information about the Current Turn in the Campaign for a specific Game.
    /// </summary>
    Task<ServiceResult<CurrentCampaignTurn>> GetCurrentCampaignTurnAsync(int gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns information about the next Turn in the Campaign for a specific Game.
    /// </summary>
    Task<ServiceResult<CurrentCampaignTurn>> GetNextCampaignTurnAsync(int gameId, int turnOrderOfLastPlayer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns information about the active player with the lowest TurnOrder in the Campaign for a specific Game.
    /// </summary>
    Task<ServiceResult<CurrentCampaignTurn>> GetFirstCampaignTurnAsync(int gameId);

    /// <summary>
    /// As <see cref="GetFirstCampaignTurnAsync(int)"/>, but reading through a context the caller supplies.
    /// </summary>
    /// <remarks>
    /// Needed by callers working inside an open transaction. The parameterless-context overload makes its own context, which means a
    /// separate connection that cannot see the caller's uncommitted writes - and would block on the rows they have locked. Pass the
    /// context that did the writing and the read joins the same transaction.
    /// </remarks>
    /// <param name="context">The database context, and therefore the transaction, to read through.</param>
    /// <param name="gameId">The Game whose Campaign Turn Queue is being read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ServiceResult<CurrentCampaignTurn>> GetFirstCampaignTurnAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current campaign round number for a Game, or <c>0</c> when the game has no active campaign turn queue
    /// (e.g. before the campaign has started).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GetCurrentCampaignTurnAsync"/>, this does not require any participant to be actively taking their turn,
    /// so it is safe to call from flows that can run outside a player's turn (e.g. saving a unit template).
    /// </remarks>
    Task<int> GetCurrentRoundNumberAsync(int gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Turn id to stamp on what a Player observes: their own current or most recent Campaign Turn, falling back to the lane's open Turn for a Player
    /// who has never taken one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reader's own Turn, not the acting Player's, because "when did <i>I</i> last see this tile" is the question the stamp answers. A Player looking
    /// at the map out of turn is looking with the eyes they had when they last acted, and their Forces have not moved since.
    /// </para>
    /// <para>
    /// The fallback matters for the first Round: a Player who has not yet taken a Turn still explores ground simply by opening the map, and the lane's open
    /// Turn is the only honest answer to "when".
    /// </para>
    /// <para>
    /// Fails when the lane has no Turns at all. That is not reachable in play: the map can only be read for a Game in progress, and a Game is marked in
    /// progress in the same transaction that opens its first Turn. Failing rather than stamping a placeholder stops a broken invariant from quietly writing a
    /// Turn id that means nothing onto every Tile the Player looks at.
    /// </para>
    /// </remarks>
    Task<ServiceResult<int>> GetObservationTurnIdAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancellationToken = default);
}