namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

/// <summary>
/// The order the start-of-Campaign-Turn processes run in. Lower values run first.
/// </summary>
/// <inheritdoc cref="EndTurn.EndCampaignTurnProcessOrder" path="/remarks"/>
internal enum StartCampaignTurnProcessOrder
{
    /// <summary>Sentries report what they can see as the Turn opens.</summary>
    AlertSentries = 10000,

    /// <summary>Supply is reckoned against the Forces in the field.</summary>
    CheckUnitSupply = 20000,
}
