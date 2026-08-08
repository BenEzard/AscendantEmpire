using AscendantEmpire.FrontendContracts.Units.GetTroopDisplayInfo;

namespace AscendantEmpire.Application.Features.Units.Classes;

public class DTTroopDisplayInfo : ITroopDisplayInfo
{
    public int UnitTemplateId { get; set; }

    public required string MapImagePath { get; set; }
}
