using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

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
    /// Entering a tile requires only half its movement cost but spends all of it, so a Force can finish a step in debt. See
    /// <c>docs/design/units/unit-mechanics.md</c>, rule 2.
    /// </remarks>
    public decimal MovementPointsRemaining { get; init; }

    /// <summary>The Force's full movement allowance for a turn, taken from its compiled template.</summary>
    public decimal CampaignMovementPoints { get; init; }

    /// <summary>
    /// The player's refreshed view of the map from where their observers now stand.
    /// </summary>
    /// <remarks>
    /// Produced by the same fog-of-war read the map endpoint uses, so a step both records the newly revealed tiles against the player's map and hands
    /// the client the tiles to patch in — including the tile the Force just left, which now shows it gone.
    /// </remarks>
    public IReadOnlyCollection<ObserverSight> Observers { get; init; } = [];
}
