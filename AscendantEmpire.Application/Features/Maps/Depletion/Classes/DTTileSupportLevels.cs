using AscendantEmpire.FrontendContracts.Biomes.GetTileSupportLevels;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

public class DTTileSupportLevels : ITileSupportLevels
{
    public IReadOnlyCollection<ITileSupportLevelEntry> Tiles { get; set; } = [];
}
