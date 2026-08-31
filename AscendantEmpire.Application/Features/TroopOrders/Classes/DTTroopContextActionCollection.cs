using AscendantEmpire.FrontendContracts.TroopOrders.GetTroopContextActions;

namespace AscendantEmpire.Application.Features.TroopOrders.Classes;

/// <inheritdoc cref="ITroopContextActionCollection"/>
public class DTTroopContextActionCollection : ITroopContextActionCollection
{
    public required IReadOnlyCollection<ITroopContextAction> Actions { get; set; }
}
