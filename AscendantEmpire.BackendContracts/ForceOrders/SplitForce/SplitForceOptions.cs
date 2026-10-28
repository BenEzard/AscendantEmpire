using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.ForceOrders.SplitForce;

/// <summary>
/// What the split dialog needs to offer a legal division of one unled Force, as the handler returns it.
/// </summary>
public record SplitForceOptions
{
    public required int UnitInstanceId { get; init; }
    public required string DisplayName { get; init; }
    public required LeadershipModel LeadershipModel { get; init; }
    public required int HealthyCount { get; init; }
    public required int InjuredCount { get; init; }
    public required int TotalStrength { get; init; }
    public required int BaseSize { get; init; }
}
