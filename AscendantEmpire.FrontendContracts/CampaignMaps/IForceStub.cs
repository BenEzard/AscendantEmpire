using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IForceStub
{
    public int Id { get; }
    public RecordType RecordType { get; }

    /// <summary>
    /// The player who owns this Force — ownership, not command.
    /// </summary>
    public int OwnerPlayerId { get; init; }

    public int HealthyTroops { get; }
    public int InjuredTroops { get; }
    public int InitialStrength { get; set; }
    public int UnitTemplateId { get; }

    /// <summary>
    /// Whether this Force still has campaign movement left this turn, or <c>null</c> when the observing player does not own it.
    /// </summary>
    public bool? HasMovementRemaining { get; }
}