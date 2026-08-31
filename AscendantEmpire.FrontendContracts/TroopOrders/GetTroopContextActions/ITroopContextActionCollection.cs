namespace AscendantEmpire.FrontendContracts.TroopOrders.GetTroopContextActions;

/// <summary>
/// The context actions available for one selected troop.
/// </summary>
public interface ITroopContextActionCollection
{
    /// <summary>The actions, already in the toolbar's fixed display order.</summary>
    IReadOnlyCollection<ITroopContextAction> Actions { get; }
}
