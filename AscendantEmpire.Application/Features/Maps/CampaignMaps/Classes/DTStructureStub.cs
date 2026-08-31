using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Structures;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

public class DTStructureStub : IStructureStub
{
    public int Id { get; set; }
    public RecordType RecordType { get; set; }
    public int OwnerPlayerId { get; init; }
    public int StructureId { get; set; }

    public PositionOnTile PositionOnTile { get; set; }
    public double CompletionPercent { get; set; }
}
