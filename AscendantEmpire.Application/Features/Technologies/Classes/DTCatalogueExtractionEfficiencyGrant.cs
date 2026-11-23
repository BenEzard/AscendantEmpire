using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTCatalogueExtractionEfficiencyGrant : ICatalogueExtractionEfficiencyGrant
{
    public required int AssetId { get; set; }

    public required string AssetCode { get; set; }

    public required string AssetDisplayName { get; set; }

    public required int FlatValue { get; set; }

    public required int PercentValue { get; set; }
}
