using AscendantEmpire.Domain.Enums.Settlements;

namespace AscendantEmpire.BackendContracts.ForceOrders.SettleForce;

/// <summary>
/// What a settle order produced, as the handler returns it.
/// </summary>
public record SettleForceResult
{
    public required int SettlementId { get; init; }
    public required int StructureInstanceId { get; init; }
    public required string? Name { get; init; }
    public required SettlementTier Tier { get; init; }
    public required int Population { get; init; }
    public required int XCoord { get; init; }
    public required int YCoord { get; init; }
    public required double CompletionPercent { get; init; }
    public required int TurnsToComplete { get; init; }
}
