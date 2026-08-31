using AscendantEmpire.Domain.Enums.Troops;

namespace AscendantEmpire.FrontendContracts.TroopOrders.GetTroopContextActions;

/// <summary>
/// One action offered for a selected troop in the context action toolbar.
/// </summary>
/// <remarks>
/// Actions the troop could never take are absent from the response entirely, so every action here is worth a button — enabled or otherwise.
/// </remarks>
public interface ITroopContextAction
{
    /// <summary>
    /// Which action this is. Serialised as the enum member's name (e.g. <c>SplitTroops</c>); the frontend keys its icon, label and interaction
    /// off it.
    /// </summary>
    TroopActionType ActionType { get; }

    /// <summary>The action's display label. A fallback the frontend uses when it does not recognise <see cref="ActionType"/>.</summary>
    string Label { get; }

    /// <summary>The action's fixed position in the toolbar. Ascending.</summary>
    int DisplayOrder { get; }

    /// <summary>Whether the action can be taken right now.</summary>
    bool IsEnabled { get; }

    /// <summary>Every reason the action is currently unavailable. Empty when <see cref="IsEnabled"/> is true.</summary>
    IReadOnlyCollection<string> DisabledReasons { get; }
}
