using AscendantEmpire.Domain.Enums.Maps;
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

    /// <summary>What the step actually cost, rivers and all, so the client can say what was paid rather than deducing it.</summary>
    decimal StepCost { get; }

    /// <summary>
    /// How much of <see cref="StepCost"/> the rivers accounted for: positive where they made the step dearer, negative where they made it cheaper.
    /// </summary>
    /// <remarks>
    /// A single net figure — one step can both cross a river and follow another — and it is the difference from what the ground alone would have cost, so
    /// it reads the same whatever the Force's movement domain.
    /// </remarks>
    decimal RiverMovementCost { get; }

    /// <summary>
    /// Whether the step took the Force onto a tile held by another Player's Forces, and so started a Battle.
    /// </summary>
    /// <remarks>
    /// When it did, the Force is now in battle and cannot take another campaign step, so the client can tell the player the fighting has begun rather than leaving them to find out from a
    /// refused move.
    /// </remarks>
    bool StartedBattle { get; }

    /// <summary>
    /// The river the step crossed from one bank to the other, or <see cref="RiverWidth.None"/>, so the client can explain what the step cost.
    /// </summary>
    /// <remarks>The step's geometry, whatever the Force's domain made of it: an Air Force crosses rivers without paying for them.</remarks>
    RiverWidth RiverCrossed { get; }

    /// <summary>
    /// The river the step ran alongside, with both tiles on the same bank, or <see cref="RiverWidth.None"/>.
    /// </summary>
    /// <remarks>For a Land Force it made the step cheaper; for a ship it is the river being sailed up.</remarks>
    RiverWidth RiverAlong { get; }

    /// <summary>Everything the player can see from where their Forces and structures now stand.</summary>
    IReadOnlyCollection<IObserver> Observers { get; }
}
