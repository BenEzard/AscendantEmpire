using AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="ITakeResourcesResult"/>
public class DTTakeResourcesResult : ITakeResourcesResult
{
    public required string DisplayName { get; set; }

    public int XCoord { get; set; }

    public int YCoord { get; set; }

    public int UnitsTaken { get; set; }

    public bool IsExhausted { get; set; }

    public IReadOnlyCollection<ITakeResourcesAssetLine> Gained { get; set; } = [];

    public IReadOnlyCollection<ITakeResourcesForceOutcome> Forces { get; set; } = [];
}

/// <inheritdoc cref="ITakeResourcesForceOutcome"/>
public class DTTakeResourcesForceOutcome : ITakeResourcesForceOutcome
{
    public int UnitInstanceId { get; set; }

    public required string DisplayName { get; set; }

    public int UnitsTaken { get; set; }
}
