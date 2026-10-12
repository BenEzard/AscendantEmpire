using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTCatalogueAcquisitionCost : ICatalogueAcquisitionCost
{
    public required int AssetId { get; set; }

    public required string AssetCode { get; set; }

    public required string AssetDisplayName { get; set; }

    public required string AssetIconPath { get; set; }

    public required int Quantity { get; set; }
}
