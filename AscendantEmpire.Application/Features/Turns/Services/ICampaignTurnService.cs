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
    /// Returns the current campaign round number for a Game, or <c>0</c> when the game has no active campaign turn queue
    /// (e.g. before the campaign has started).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GetCurrentCampaignTurnAsync"/>, this does not require any participant to be actively taking their turn,
    /// so it is safe to call from flows that can run outside a player's turn (e.g. saving a unit template).
    /// </remarks>
    Task<int> GetCurrentRoundNumberAsync(int gameId, CancellationToken cancellationToken = default);
}