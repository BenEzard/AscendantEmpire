using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface ITroopStub
{
    public int Id { get; }
    public RecordType RecordType { get; }

    /// <summary>
    /// The player who owns this troop — ownership, not command.
    /// </summary>
    public int OwnerPlayerId { get; init; }

    public int HealthyTroops { get; }
    public int InjuredTroops { get; }
    public int InitialStrength { get; set; }
    public int UnitTemplateId { get; }
}