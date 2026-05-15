namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;

/// <summary>
/// A single check that evaluates whether a player has outstanding actions of a specific kind.
/// Each feature area implements its own check(s).
/// </summary>
internal interface IPossibleActionCheck
{
    /// <summary>
    /// Descriptive name for logging/diagnostics.
    /// </summary>
    string CheckName { get; }

    /// <summary>
    /// Execution order. Lower values run first.
    /// </summary>
    int ExecutionOrder { get; }

    /// <summary>
    /// Evaluate pending actions for the given player/game context.
    /// Implementations should return an empty list (not null) when nothing is outstanding.
    /// </summary>
    Task<IReadOnlyList<PossibleAction>> EvaluateAsync(
        PossibleActionContext context,
        CancellationToken cancellationToken);
}
