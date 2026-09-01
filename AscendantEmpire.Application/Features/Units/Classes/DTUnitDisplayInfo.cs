using AscendantEmpire.FrontendContracts.Units.GetUnitDisplayInfo;

namespace AscendantEmpire.Application.Features.Units.Classes;

public class DTUnitDisplayInfo : IUnitDisplayInfo
{
    public int UnitTemplateId { get; set; }

    public required string MapImagePath { get; set; }

    public required string DisplayName { get; set; }
}
