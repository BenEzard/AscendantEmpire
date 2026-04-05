using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IStructureStub
{
    public int Id { get; }
    public int StructureId { get; }
    public int OwnerId { get; }
    public PositionOnTile PositionOnTile { get; }
    public double CompletionPercent { get; }
}
