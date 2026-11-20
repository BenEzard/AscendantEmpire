namespace AscendantEmpire.BackendContracts.ForceOrders.TakeResources;

/// <summary>
/// What a take-resources order produced, as the handler returns it.
/// </summary>
public record TakeResourcesResult
{
    public required string DisplayName { get; init; }
    public required int XCoord { get; init; }
    public required int YCoord { get; init; }
    public required int UnitsTaken { get; init; }
    public required bool IsExhausted { get; init; }
    public required IReadOnlyCollection<TakeResourcesAssetLine> Gained { get; init; }
    public required IReadOnlyCollection<TakeResourcesForceOutcome> Forces { get; init; }
}

/// <summary>
/// One Force's share of a take-resources order, as the handler returns it.
/// </summary>
public record TakeResourcesForceOutcome
{
    public required int UnitInstanceId { get; init; }
    public required string DisplayName { get; init; }
    public required int UnitsTaken { get; init; }
}
