using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record ObservedForce
{
    public int Id { get; init; }
    public RecordType RecordType { get; init; }

    /// <summary>
    /// The player who owns this Force.
    /// </summary>
    /// <remarks>
    /// Named for ownership, not command. It was previously <c>CommanderId</c>, which was misleading even then — it has only ever been populated
    /// with the owning player's id — and becomes actively wrong once a Command carries a real commanding officer.
    /// </remarks>
    public int OwnerPlayerId { get; init; }

    public int UnitTemplateId { get; init; }

    /// <summary>
    /// The number of individual soldiers in this Force who are unwounded.
    /// </summary>
    public int HealthyTroops { get; init; }

    /// <summary>
    /// The number of individual soldiers in this Force who are wounded.
    /// </summary>
    public int InjuredTroops { get; init; }

    /// <summary>
    /// The total number of Forces when it was first created.
    /// </summary>
    public int InitialStrength { get; init; }
}
