namespace AscendantEmpire.BackendContracts.ForceOrders.SplitForce;

/// <summary>
/// One of the Forces a completed split produced, as the handler returns it.
/// </summary>
public record SplitForceGroup
{
    public required int UnitInstanceId { get; init; }
    public required int Size { get; init; }
    public required int HealthyCount { get; init; }
    public required int InjuredCount { get; init; }
    public required bool IsOriginalForce { get; init; }
}
