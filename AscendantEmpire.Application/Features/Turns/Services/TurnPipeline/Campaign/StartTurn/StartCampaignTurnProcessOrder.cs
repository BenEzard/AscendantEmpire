namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

/// <summary>
/// The order the start-of-Campaign-Turn processes run in. Lower values run first.
/// </summary>
/// <inheritdoc cref="EndTurn.EndCampaignTurnProcessOrder" path="/remarks"/>
internal enum StartCampaignTurnProcessOrder
{
    /// <summary>Sentries report what they can see as the Turn opens.</summary>
    AlertSentries = 10_000,

    /// <summary>Supply is reckoned against the Forces in the field.</summary>
    CheckUnitSupply = 20_000,

    /// <summary>The Turn's movement allowance is handed out, once supply has settled who is entitled to one.</summary>
    /// <remarks>
    /// Deliberately after <see cref="CheckUnitSupply"/>. A Unit whose body requires fuel it does not have receives no campaign movement at all, and
    /// that verdict has to exist before the allowance is granted rather than being unpicked afterwards.
    /// </remarks>
    ReplenishMovementPoints = 30_000,
}
