using AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="ISplitForceResult"/>
public class DTSplitForceResult : ISplitForceResult
{
    public int XCoord { get; set; }

    public int YCoord { get; set; }

    public IReadOnlyCollection<ISplitForceGroup> Groups { get; set; } = [];
}
