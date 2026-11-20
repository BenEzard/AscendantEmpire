using AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="ITakeResourcesAssetLine"/>
public class DTTakeResourcesAssetLine : ITakeResourcesAssetLine
{
    public int AssetId { get; set; }

    public required string AssetDisplayName { get; set; }

    public int Amount { get; set; }
}
