using AscendantEmpire.FrontendContracts.Units.GetTroopDisplayInfo;

namespace AscendantEmpire.Application.Features.Units.Classes;

public class DTTroopDisplayInfoCollection : ITroopDisplayInfoCollection
{
    public required IReadOnlyCollection<ITroopDisplayInfo> TroopDisplayInfo { get; set; }
}
