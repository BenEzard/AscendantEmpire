using AscendantEmpire.Domain.Enums.Troops;

namespace AscendantEmpire.BackendContracts.TroopOrders.GetTroopContextActions;

/// <summary>
/// One action offered for a selected troop, and whether the player may take it right now.
/// </summary>
/// <remarks>
/// Actions the troop could never take are absent entirely rather than present and disabled, so everything in a list of these is worth showing.
/// </remarks>
public record TroopContextAction
{
    /// <summary>Which action this is. The frontend keys its icon, label and interaction off this.</summary>
    public TroopActionType ActionType { get; init; }

    /// <summary>
    /// The action's display label, e.g. "Split Troops".
    /// </summary>
    /// <remarks>The frontend prefers its own label for action types it recognises; this is the fallback that lets a newly added action type
    /// render sensibly before the frontend knows about it.</remarks>
    public required string Label { get; init; }

    /// <summary>
    /// The action's fixed position in the toolbar, taken from <see cref="ActionType"/>'s numeric value.
    /// </summary>
    /// <remarks>Sent explicitly so the ordering survives the enum being serialised by name, and so the toolbar's sequence cannot drift with
    /// dependency-injection registration order.</remarks>
    public int DisplayOrder { get; init; }

    /// <summary>Whether the action can be taken right now.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Every reason the action is currently unavailable. Empty when <see cref="IsEnabled"/> is true.
    /// </summary>
    public IReadOnlyCollection<string> DisabledReasons { get; init; } = [];
}
