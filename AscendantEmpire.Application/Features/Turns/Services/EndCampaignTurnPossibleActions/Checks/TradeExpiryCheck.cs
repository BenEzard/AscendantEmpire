namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

/// <summary>
/// Flags trade deals that will expire at end of this round if not renewed.
/// Placeholder implementation until the diplomacy/trade feature is built.
/// </summary>
internal sealed class TradeExpiryCheck : IPossibleActionCheck
{
    public string CheckName => "Trade Deal Expiry";
    public int ExecutionOrder => 300;

    public Task<IReadOnlyList<PossibleAction>> EvaluateAsync(
        PossibleActionContext context,
        CancellationToken cancellationToken)
    {
        // TODO: Query active trade deals expiring this round once the feature exists.
        IReadOnlyList<PossibleAction> empty = [];
        return Task.FromResult(empty);
    }
}