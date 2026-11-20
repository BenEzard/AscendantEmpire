using AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

namespace AscendantEmpire.BackendContracts.ForceOrders.TakeResources;

/// <inheritdoc cref="ITakeResourcesDto"/>
public record class TakeResourcesDto : ITakeResourcesDto
{
    public IReadOnlyCollection<int> ForceIds { get; init; } = [];

    public int MapResourceInstanceId { get; init; }
}
