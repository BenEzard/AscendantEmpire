namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;

/// <summary>
/// The order the end-of-Campaign-Round processes run in. Lower values run first.
/// </summary>
/// <inheritdoc cref="EndTurn.EndCampaignTurnProcessOrder" path="/remarks"/>
internal enum EndCampaignRoundProcessOrder
{
    /// <summary>The wounded recover before anything is judged on the Round's final state.</summary>
    HealTroops = 10000,

    /// <summary>Depleted tiles regrow once every Player's Forces have fed for the Round, so the next Round opens on the regrown amounts.</summary>
    RegenerateTileSustenance = 15000,

    /// <summary>The Round's borders are settled on the state the rest of the Round left behind, and before anyone is judged to have won on them.</summary>
    ResolveTileTerritory = 17500,

    /// <summary>Whether anyone has won is decided on the state the rest of the end-of-Round processing leaves behind, so this runs last.</summary>
    EvaluateVictoryConditions = 20000,
}
