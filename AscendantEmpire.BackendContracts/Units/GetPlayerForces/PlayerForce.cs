using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

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

    /// <summary>
    /// Which terrain this Force can cross, from its compiled template.
    /// </summary>
    /// <remarks>
    /// Carried for the fog-of-war read as well as for movement: an Observer blinded by dense terrain reveals the adjacent Tiles its domain could
    /// never have entered, so the sight pipeline has to know which those are. Defaults to <see cref="DomainType.Land"/> so a projection that forgets
    /// to set it describes a foot soldier rather than something that goes anywhere.
    /// </remarks>
    public DomainType MovementDomain { get; set; } = DomainType.Land;
    public int HealthyTroops { get; set; }
    public int InjuredTroops { get; set; }
    public int InitialStrength { get; set; }
    public required string MapImagePath { get; set; }

    /// <summary>
    /// Whether this Force still has campaign movement left to spend this turn.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="Domain.Entities.Units.UnitInstance.CampaignMovementRemaining"/>, which is a raw fixed-point value on the Scale 1,000
    /// convention. The boolean is what travels, so no consumer has to know that convention and the threshold stays a backend decision.
    /// <para>
    /// A display hint, never authorisation. Whether a Force may actually take an action is decided by the context-action pipeline against a freshly built
    /// context, not by this flag.
    /// </para>
    /// </remarks>
    public bool HasCampaignMovementRemaining { get; set; }

    /// <summary>
    /// The campaign movement points this Force has left this turn.
    /// </summary>
    /// <remarks>
    /// The same value <see cref="HasCampaignMovementRemaining"/> is derived from, converted out of the raw Scale 1,000 representation so the convention stays
    /// behind the API. Unmasked here — this projection covers every Force on a tile, and it is the fog-of-war read that decides whose numbers the
    /// observing player is allowed to see.
    /// <para>May be negative: entering a tile costs half its movement cost but spends all of it.</para>
    /// <para>May also exceed <see cref="CampaignMovementPoints"/>: unspent movement is carried into the next turn and added on top of the allowance.</para>
    /// </remarks>
    public decimal CampaignMovementRemaining { get; set; }

    /// <summary>
    /// This Force's full movement allowance for a turn, from its compiled template.
    /// </summary>
    public decimal CampaignMovementPoints { get; set; }

    /// <summary>
    /// The battle movement points this Force has left in its current Battle.
    /// </summary>
    /// <remarks>
    /// A separate pool from <see cref="CampaignMovementRemaining"/>, held so a Force drawn into a Battle keeps the campaign movement it will need once
    /// the Battle releases it. Granted when the Unit is fielded and not yet replenished by anything, so today it only decreases when a Battle spends it.
    /// </remarks>
    public decimal BattleMovementRemaining { get; set; }
}
