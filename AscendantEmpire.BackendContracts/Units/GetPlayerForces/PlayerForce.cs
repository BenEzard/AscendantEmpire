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
}
