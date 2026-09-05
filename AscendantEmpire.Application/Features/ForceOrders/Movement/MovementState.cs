namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// Current per-turn movement state for a Force instance.
/// This is intentionally separated from <see cref="MovementProfile"/> because it changes as a
/// Force spends and regains movement over time.
/// </summary>
public sealed record MovementState
{
    public required decimal CampaignMovementRemaining { get; init; }
}
