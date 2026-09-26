using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.ForceOrders.StepForce;

/// <summary>
/// The outcome of a successful single-tile step: where the Force ended up, what it cost, and what the player can now see.
/// </summary>
/// <remarks>
/// Movement points cross this boundary as decimals. They are stored raw on the Scale 1,000 fixed-point convention, and that convention stays behind
/// the API so no consumer has to know it.
/// </remarks>
public record StepForceResult
{
    public int UnitInstanceId { get; init; }

    public int XCoord { get; init; }

    public int YCoord { get; init; }

    /// <summary>
    /// The movement points remaining after the step, which may be negative.
    /// </summary>
    /// <remarks>
    /// A Force holding any movement at all may enter a tile and pays its whole cost, so it can finish a step in debt. See the entry gate in
    /// <c>docs/design/units/unit-movement.md</c>.
    /// </remarks>
    public decimal CampaignMovementRemaining { get; init; }

    /// <summary>The Force's full movement allowance for a turn, taken from its compiled template.</summary>
    public decimal CampaignMovementPoints { get; init; }

    /// <summary>
    /// Whether the step took the Force onto a tile held by another Player's Forces, and so started a Battle.
    /// </summary>
    /// <remarks>
    /// When it did, the Force and every enemy Force on the tile are now in battle, and none of them can take a campaign step until it is over.
    /// </remarks>
    public bool StartedBattle { get; init; }

    /// <summary>
    /// The river the step crossed from one bank to the other, or <see cref="RiverWidth.None"/>.
    /// </summary>
    /// <remarks>
    /// The geometry of the step, whatever the Force's domain made of it: an Air Force that crossed a river is told so too, though it paid nothing for it.
    /// </remarks>
    public RiverWidth RiverCrossed { get; init; }

    /// <summary>
    /// The river the step ran alongside, with both tiles on the same bank, or <see cref="RiverWidth.None"/>.
    /// </summary>
    /// <remarks>The geometry of the step, like <see cref="RiverCrossed"/>. For a Land Force it made the step cheaper; for a ship it is the river sailed up.</remarks>
    public RiverWidth RiverAlong { get; init; }

    /// <summary>
    /// The player's refreshed view of the map from where their observers now stand.
    /// </summary>
    /// <remarks>
    /// Produced by the same fog-of-war read the map endpoint uses, so a step both records the newly revealed tiles against the player's map and hands
    /// the client the tiles to patch in — including the tile the Force just left, which now shows it gone.
    /// </remarks>
    public IReadOnlyCollection<ObserverSight> Observers { get; init; } = [];
}
