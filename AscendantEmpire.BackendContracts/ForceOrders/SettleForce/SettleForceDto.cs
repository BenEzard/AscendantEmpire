using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.FrontendContracts.ForceOrders.SettleForce;

namespace AscendantEmpire.BackendContracts.ForceOrders.SettleForce;

public record class SettleForceDto : ISettleForceDto
{
    public int UnitInstanceId { get; init; }
    public SettlementTier Tier { get; init; }
    public string? Name { get; init; }
}
