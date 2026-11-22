namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;

/// <summary>
/// The order the start-of-Campaign-Round processes run in. Lower values run first.
/// </summary>
/// <inheritdoc cref="EndRound.EndCampaignRoundProcessOrder" path="/remarks"/>
internal enum StartCampaignRoundProcessOrder
{
    /// <summary>
    /// Settlements draw from their territory before anybody moves: the tiles have just regrown at the end of the last Round, and a Round gathers its
    /// resources before anything is fed out of them.
    /// </summary>
    HarvestSettlementTerritory = 10000,
}
