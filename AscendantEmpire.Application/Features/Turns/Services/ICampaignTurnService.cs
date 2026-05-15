using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

internal interface ICampaignTurnService
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
}