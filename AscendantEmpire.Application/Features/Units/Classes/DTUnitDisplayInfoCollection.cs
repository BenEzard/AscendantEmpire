using AscendantEmpire.FrontendContracts.Units.GetUnitDisplayInfo;

namespace AscendantEmpire.Application.Features.Units.Classes;

public class DTUnitDisplayInfoCollection : IUnitDisplayInfoCollection
{
    public required IReadOnlyCollection<IUnitDisplayInfo> UnitDisplayInfo { get; set; }
}
