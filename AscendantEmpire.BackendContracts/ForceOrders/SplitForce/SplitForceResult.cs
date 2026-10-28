namespace AscendantEmpire.BackendContracts.ForceOrders.SplitForce;

/// <summary>
/// What a split order produced, as the handler returns it.
/// </summary>
public record SplitForceResult
{
    public required int XCoord { get; init; }
    public required int YCoord { get; init; }
    public required IReadOnlyCollection<SplitForceGroup> Groups { get; init; }
}
