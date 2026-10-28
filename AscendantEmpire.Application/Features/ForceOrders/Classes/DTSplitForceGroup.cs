using AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="ISplitForceGroup"/>
public class DTSplitForceGroup : ISplitForceGroup
{
    public int UnitInstanceId { get; set; }

    public int Size { get; set; }

    public int HealthyCount { get; set; }

    public int InjuredCount { get; set; }

    public bool IsOriginalForce { get; set; }
}
