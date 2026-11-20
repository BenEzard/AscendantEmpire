namespace AscendantEmpire.BackendContracts.ForceOrders.TakeResources;

/// <summary>
/// What the take-resources dialog needs to offer a choice, as the handler returns it.
/// </summary>
public record TakeResourcesOptions
{
    public required IReadOnlyCollection<TakeResourcesOption> Resources { get; init; }
}

/// <summary>
/// One MapResource on the tile the selection could take from, as the handler returns it.
/// </summary>
public record TakeResourcesOption
{
    public required int MapResourceInstanceId { get; init; }
    public required byte Slot { get; init; }
    public required string DisplayName { get; init; }
    public required string? IconPath { get; init; }
    public required int UnitsWouldTake { get; init; }
    public required IReadOnlyCollection<TakeResourcesAssetLine> WouldGain { get; init; }
}
