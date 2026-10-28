using AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

namespace AscendantEmpire.BackendContracts.ForceOrders.SplitForce;

/// <inheritdoc cref="ISplitForceDto"/>
public record class SplitForceDto : ISplitForceDto
{
    public int UnitInstanceId { get; init; }

    public IReadOnlyCollection<int> GroupSizes { get; init; } = [];
}
