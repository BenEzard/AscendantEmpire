namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;

/// <summary>
/// Aggregates all registered <see cref="IPossibleActionCheck"/> implementations and returns a list of possible actions for a Player.
/// </summary>
internal interface IEndCampaignTurnPossibleActionService
{
    Task<IReadOnlyList<PossibleAction>> GetPossibleActionsAsync(
        PossibleActionContext context,
        CancellationToken cancellationToken);
}
