using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.Units.GetPlayerForces;

public class PlayerForce
{
    public int UnitInstanceId { get; set; }
    public RecordType RecordType { get; set; }
    public int PlayerId { get; set; }
    public int CampaignMapTileId { get; set; }
    public int? BattleMapTileId { get; set; }
    public int CampaignXCoord { get; set; }
    public int CampaignYCoord { get; set; }
    public int UnitTemplateId { get; set; }
    public int SightRange { get; set; }
    public int HealthyTroops { get; set; }
    public int InjuredTroops { get; set; }
    public int InitialStrength { get; set; }
    public required string MapImagePath { get; set; }

    /// <summary>
    /// Whether this Force still has campaign movement left to spend this turn.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="Domain.Entities.Units.UnitInstance.MovementPointsRemaining"/>, which is a raw fixed-point value on the Scale 1,000
    /// convention. The boolean is what travels, so no consumer has to know that convention and the threshold stays a backend decision.
    /// <para>
    /// A display hint, never authorisation. Whether a Force may actually take an action is decided by the context-action pipeline against a freshly built
    /// context, not by this flag.
    /// </para>
    /// </remarks>
    public bool HasMovementRemaining { get; set; }

    /// <summary>
    /// The campaign movement points this Force has left this turn.
    /// </summary>
    /// <remarks>
    /// The same value <see cref="HasMovementRemaining"/> is derived from, converted out of the raw Scale 1,000 representation so the convention stays
    /// behind the API. Unmasked here — this projection covers every Force on a tile, and it is the fog-of-war read that decides whose numbers the
    /// observing player is allowed to see.
    /// <para>May be negative: entering a tile costs half its movement cost but spends all of it.</para>
    /// </remarks>
    public decimal MovementPointsRemaining { get; set; }

    /// <summary>
    /// This Force's full movement allowance for a turn, from its compiled template.
    /// </summary>
    public decimal CampaignMovementPoints { get; set; }
}
