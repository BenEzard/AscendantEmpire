namespace AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

/// <summary>
/// What the take-resources dialog needs to offer a choice: every MapResource the selection could take from, and what each would give.
/// </summary>
/// <remarks>
/// <para>
/// Fetched when the dialog opens rather than carried on the context-actions list endpoint, for the same reason the split options are: that endpoint says
/// only whether an action is available, and a per-action payload on it would make every action's contract the union of every action's needs. See
/// <c>docs/design/units/force-context-action-toolbar.md</c>, "Splitting needs parameters".
/// </para>
/// <para>
/// Judged for the <b>whole selection</b>, so the figures are what these Forces together would come away with, not what one of them would.
/// </para>
/// </remarks>
public interface ITakeResourcesOptions
{
    /// <summary>
    /// What is takeable on the tile the selection stands on, in tile slot order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tile is not named. The dialog opens over a selection that is already standing somewhere, so coordinates would only be a second place for the
    /// client and the server to disagree about where that is.
    /// </para>
    /// <para>
    /// Empty where nothing is - which the toolbar would already have hidden the button for, so a dialog seeing it has been opened against a tile that
    /// changed under it. A MapResource holding its amount in child rows is never here: a Distribution and a stratified instance have no quantity to take
    /// against, and neither is takeable yet.
    /// </para>
    /// </remarks>
    IReadOnlyCollection<ITakeResourcesOption> Resources { get; }
}
