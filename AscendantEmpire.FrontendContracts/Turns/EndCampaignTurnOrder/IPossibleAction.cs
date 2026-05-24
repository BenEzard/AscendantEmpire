using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

public interface IPossibleAction
{
    public InformationCategory Category { get; }
    public string Description { get; }
    public int XCoord { get; }
    public int YCoord { get; }
    public int Id { get; }
    public int RecordTypeId { get; }
}
