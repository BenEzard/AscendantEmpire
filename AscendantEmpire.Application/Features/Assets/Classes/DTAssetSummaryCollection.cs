using AscendantEmpire.FrontendContracts.Assets.GetAssets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

public class DTAssetSummaryCollection : IAssetSummaryCollection
{
    public required IReadOnlyCollection<IAssetSummary> Assets { get; set; }
}

public class DTAssetSummary : IAssetSummary
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required string MetaCategoryDisplayName { get; set; }

    public required bool IsNaturallyOccurring { get; set; }
}
