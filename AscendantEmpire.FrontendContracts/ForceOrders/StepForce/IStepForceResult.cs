using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.FrontendContracts.ForceOrders.StepForce;

/// <summary>
/// What changed as a result of a Force stepping one tile.
/// </summary>
/// <remarks>
/// <para>
/// A step both moves the Force and shifts what its owner can see, so the newly observed tiles travel back on the same response rather than needing
/// a second round-trip. That is what lets the client patch the tiles it already holds instead of reloading the map.
/// </para>
/// <para>
/// <see cref="Observers"/> carries the same shape the fog-of-war read returns, so both responses can be merged into the client's tile cache by the
/// same code. It covers every observer the player owns, not only the Force that moved — a step can uncover ground for a neighbouring Force too.
/// </para>
/// </remarks>
public interface IStepForceResult
{
    /// <summary>The Force that moved.</summary>
    int UnitInstanceId { get; }

    /// <summary>The Force's new X coordinate.</summary>
    int XCoord { get; }

    /// <summary>The Force's new Y coordinate.</summary>
    int YCoord { get; }

    /// <summary>
    /// The movement points the Force has left after paying for the step.
    /// </summary>
    /// <remarks>
    /// May be negative. A Force holding any movement at all may enter a tile and pays its whole cost, so a slow Force crossing expensive ground ends
    /// the turn in debt and spends the next one paying it off. See <c>docs/design/units/unit-movement.md</c>.
    /// </remarks>
    decimal CampaignMovementRemaining { get; }

    /// <summary>The Force's full movement allowance for a turn, so the client can show what remains as a proportion of it.</summary>
    decimal CampaignMovementPoints { get; }

    /// <summary>Everything the player can see from where their Forces and structures now stand.</summary>
    IReadOnlyCollection<IObserver> Observers { get; }
}
