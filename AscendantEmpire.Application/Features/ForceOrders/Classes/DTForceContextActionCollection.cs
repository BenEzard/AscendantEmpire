using AscendantEmpire.FrontendContracts.ForceOrders.GetForceContextActions;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="IForceContextActionCollection"/>
public class DTForceContextActionCollection : IForceContextActionCollection
{
    public required IReadOnlyCollection<IForceContextAction> Actions { get; set; }
}
