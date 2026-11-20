namespace AscendantEmpire.BackendContracts.ForceOrders.TakeResources;

/// <summary>
/// One Asset a take put, or would put, into cargo, as the handler returns it.
/// </summary>
/// <remarks>Shared by the options read and the order's result, so what the dialog promised and what arrived are described the same way.</remarks>
public record TakeResourcesAssetLine
{
    public required int AssetId { get; init; }
    public required string AssetDisplayName { get; init; }
    public required int Amount { get; init; }
}
