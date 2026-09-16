using AscendantEmpire.Domain.Enums.Resources;
using AscendantEmpire.FrontendContracts.Biomes.GetTileSupportLevels;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

public class DTTileSupportLevelEntry : ITileSupportLevelEntry
{
    public int X { get; set; }

    public int Y { get; set; }

    public TileSupportLevel SupportLevel { get; set; }
}
