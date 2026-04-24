namespace AscendantEmpire.Application.Features.TroopOrders.Movement;

/// <summary>
/// Current per-turn movement state for a troop instance.
/// This is intentionally separated from <see cref="MovementProfile"/> because it changes as a
/// troop spends and regains movement over time.
/// </summary>
public sealed record MovementState
{
    public required decimal MovementPointsRemaining { get; init; }
}
