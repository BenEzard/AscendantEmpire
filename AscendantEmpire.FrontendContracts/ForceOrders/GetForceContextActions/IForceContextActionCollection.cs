namespace AscendantEmpire.FrontendContracts.ForceOrders.GetForceContextActions;

/// <summary>
/// The context actions available for one selected troop.
/// </summary>
public interface IForceContextActionCollection
{
    /// <summary>The actions, already in the toolbar's fixed display order.</summary>
    IReadOnlyCollection<IForceContextAction> Actions { get; }
}
