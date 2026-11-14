using AscendantEmpire.FrontendContracts.Biomes.GetTileSustenance;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Classes;

public class DTTileFoodContribution : ITileFoodContribution
{
    public required string Label { get; set; }

    public decimal IndividualsFed { get; set; }

    public bool IsEstimated { get; set; }
}
