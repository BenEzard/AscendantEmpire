using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.FrontendContracts.ForceOrders.SettleForce;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="ISettleForceResult"/>
public class DTSettleForceResult : ISettleForceResult
{
    public int SettlementId { get; set; }
    public int StructureInstanceId { get; set; }
    public required string? Name { get; set; }
    public SettlementTier Tier { get; set; }
    public int Population { get; set; }
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public double CompletionPercent { get; set; }
    public int TurnsToComplete { get; set; }
}
