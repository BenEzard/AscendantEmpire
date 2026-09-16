using AscendantEmpire.FrontendContracts.Biomes.GetTileSustenance;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Classes;

public class DTTileSustenance : ITileSustenance
{
    public bool IsObserved { get; set; }

    public decimal? Forage { get; set; }

    public decimal? Hunt { get; set; }

    public decimal? Fish { get; set; }

    public decimal? Fodder { get; set; }
}
