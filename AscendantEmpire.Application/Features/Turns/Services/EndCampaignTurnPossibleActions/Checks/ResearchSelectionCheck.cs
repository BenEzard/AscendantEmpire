using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

/// <summary>
/// Flags when the player has no active research project selected.
/// Placeholder implementation until the research feature is built.
/// </summary>
internal sealed class ResearchSelectionCheck : IPossibleActionCheck
{
    public string CheckName => "Research Selection";
    public int ExecutionOrder => (int)EndTurnPossibleActionOrder.AreWeResearchingSomething;

    public Task<IReadOnlyList<PossibleAction>> EvaluateAsync(
        PossibleActionContext context,
        CancellationToken cancellationToken)
    {
        // TODO: Query actual research state once the feature exists.
        // For now, assume no Possible action.
        IReadOnlyList<PossibleAction> empty = [];
        return Task.FromResult(empty);
    }
}