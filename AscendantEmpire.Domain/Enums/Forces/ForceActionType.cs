namespace AscendantEmpire.Domain.Enums.Forces;

/// <summary>
/// An action the player can take against a selected Force <b>right now</b>, as offered by the context action toolbar.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately separate from <see cref="ForceOrderType"/>. That enum describes orders which <b>span multiple turns</b> — a persisted
/// <c>ForceOrder</c> row carrying progress, movement steps and a status the turn processor advances. A context action is something the player
/// does immediately. Executing one <i>may</i> raise a Force order (choosing to move would), but it need not: splitting resolves there and then
/// and consumes the turn without ever becoming a standing order.
/// </para>
/// <para>
/// The numeric values are intentionally <b>not</b> aligned with <see cref="ForceOrderType"/>'s, so that neither enum can be cast to the other.
/// Where an action does correspond to an order, that is a mapping the executing handler performs explicitly.
/// </para>
/// <para>
/// Values are spaced in hundreds and double as the toolbar's <b>static display order</b>, so the sequence the player sees cannot drift with
/// dependency-injection registration order.
/// </para>
/// </remarks>
public enum ForceActionType
{
    None = 0,

    // 1–999 is reserved for frontend-only affordances which still need a stable slot in the display order but have no backend rule behind them
    // (for example "Select Force", which reopens the multi-Force selection dialog). The backend never emits a value from this band.

    /// <summary>
    /// Divide the selected Force into two or more smaller ones. Costs it the entire turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named for the Force rather than the Unit deliberately. Both leadership models split at Unit level — a commissioned Unit detaches whole
    /// formations along its structure's tier lines, an unled one peels off whole multiples of its tag's base quantum — so <c>SplitUnit</c> would
    /// not have distinguished the two cases it appeared to. The player's intent is the single thing "divide what I have selected", so this is one
    /// action and one button, and <c>SplitForceStrategy</c> branches internally on the record type and the leadership model.
    /// </para>
    /// <para>
    /// That branching must stay <b>inside</b> the one strategy. <c>ForceContextActionService</c> aggregates one strategy per action type, so a
    /// second strategy claiming <see cref="SplitForce"/> would break both the ordering and the aggregation.
    /// </para>
    /// </remarks>
    SplitForce = 1000
}
