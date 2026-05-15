using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;

/// <summary>
/// The EndCampaignTurnPossibleActionService is responsible for aggregating possible actions a Player may wish to take before ending their Turn.
/// </summary>
internal sealed class EndCampaignTurnPossibleActionService(
    IEnumerable<IPossibleActionCheck> checks,
    ILogger<EndCampaignTurnPossibleActionService> logger) : IEndCampaignTurnPossibleActionService
{
    public async Task<IReadOnlyList<PossibleAction>> GetPossibleActionsAsync(
        PossibleActionContext context,
        CancellationToken cancellationToken)
    {
        var results = new List<PossibleAction>();
        
        // Execute checks in defined order so that expensive checks can be placed last
        foreach (IPossibleActionCheck check in checks.OrderBy(c => c.ExecutionOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IReadOnlyList<PossibleAction> actions = await check.EvaluateAsync(context, cancellationToken);

                if (actions.Count > 0)
                {
                    logger.LogDebug("Possible action check '{CheckName}' returned {Count} action(s) for PlayerId {PlayerId}, GameId {GameId}.",
                        check.CheckName, actions.Count, context.PlayerId, context.GameId);

                    results.AddRange(actions);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the entire pipeline for one broken check.
                // The player can still end their turn even if one subsystem errors.
                logger.LogError(
                    ex,
                    "Possible action check '{CheckName}' threw an exception for PlayerId {PlayerId}, GameId {GameId}.",
                    check.CheckName, context.PlayerId, context.GameId);
            }
        }

        return results;
    }
}