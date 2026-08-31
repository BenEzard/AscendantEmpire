namespace AscendantEmpire.Domain.Enums.Troops;

/// <summary>
/// An action the player can take against a selected troop <b>right now</b>, as offered by the context action toolbar.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately separate from <see cref="TroopOrderType"/>. That enum describes orders which <b>span multiple turns</b> — a persisted
/// <c>TroopOrder</c> row carrying progress, movement steps and a status the turn processor advances. A context action is something the player
/// does immediately. Executing one <i>may</i> raise a troop order (choosing to move would), but it need not: splitting resolves there and then
/// and consumes the turn without ever becoming a standing order.
/// </para>
/// <para>
/// The numeric values are intentionally <b>not</b> aligned with <see cref="TroopOrderType"/>'s, so that neither enum can be cast to the other.
/// Where an action does correspond to an order, that is a mapping the executing handler performs explicitly.
/// </para>
/// <para>
/// Values are spaced in hundreds and double as the toolbar's <b>static display order</b>, so the sequence the player sees cannot drift with
/// dependency-injection registration order.
/// </para>
/// </remarks>
public enum TroopActionType
{
    None = 0,

    // 1–999 is reserved for frontend-only affordances which still need a stable slot in the display order but have no backend rule behind them
    // (for example "Select Troop", which reopens the multi-troop selection dialog). The backend never emits a value from this band.

    /// <summary>
    /// Divide an unled troop into two or more smaller troops. Costs the troop its entire turn.
    /// </summary>
    SplitTroops = 1000
}
