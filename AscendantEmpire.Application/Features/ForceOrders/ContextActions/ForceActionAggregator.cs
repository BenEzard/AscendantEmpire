namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// Folds one strategy's per-Force verdicts into the single verdict the toolbar renders for the whole selection.
/// </summary>
/// <remarks>
/// <para>
/// Strategies stay per-Force — <see cref="IForceActionStrategy.Evaluate"/> judges one <see cref="ForceActionContext"/> and knows nothing about
/// selections. Everything multi-Force lives here instead, which is why widening the toolbar to a selection cost the rule layer no changes at all.
/// </para>
/// <para>
/// Pure and static so it needs no database, and shared by both the list endpoint and the execute-time re-check — the same reasoning that keeps one
/// strategy serving the read and the write. If the two aggregated differently, an action could render enabled and then be refused.
/// </para>
/// </remarks>
internal static class ForceActionAggregator
{
    /// <summary>
    /// Judges one action against every selected Force at once.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> when the action is structurally irrelevant to <b>every</b> selected Force, and so should be left out of the toolbar
    /// altogether. Otherwise an availability that is enabled only when every Force can take the action, and blocked with the collected reasons when
    /// any of them cannot.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The three outcomes are deliberately not symmetrical. An action is <b>hidden</b> only when no selected Force could ever take it: hiding as
    /// soon as one Force could not would make buttons vanish as the player adds Forces, with nothing said about why. An action is <b>enabled</b>
    /// only when every Force can take it, because pressing it is one order over the whole selection and a partial order is not what was asked for.
    /// Everything between the two is <b>disabled with reasons</b>, which is the state that can actually explain itself.
    /// </para>
    /// <para>
    /// A Force the action is structurally irrelevant to still contributes a reason here, rather than being passed over. Silently ignoring it would
    /// leave the player looking at a disabled button whose listed reasons all name other Forces.
    /// </para>
    /// </remarks>
    public static ForceActionAvailability? Aggregate(
        IForceActionStrategy strategy,
        IReadOnlyCollection<(ForceActionContext Context, ForceActionAvailability? Verdict)> verdicts)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(verdicts);

        if (verdicts.Count == 0)
        {
            return null;
        }

        // Structurally irrelevant to all of them, so there is nothing to show and nothing worth explaining.
        if (verdicts.All(verdict => verdict.Verdict is null))
        {
            return null;
        }

        List<string> reasons = [];

        foreach ((ForceActionContext context, ForceActionAvailability? verdict) in verdicts)
        {
            if (verdict is null)
            {
                // Phrased to match what EvaluateActionAsync already says when a single Force is handed an action that does not apply to it, so the
                // player meets one wording for one situation.
                reasons.Add($"'{strategy.Label}' does not apply to {context.SelectedForce.DisplayName}.");

                continue;
            }

            if (!verdict.IsEnabled)
            {
                reasons.AddRange(verdict.DisabledReasons);
            }
        }

        if (reasons.Count == 0)
        {
            return ForceActionAvailability.Available();
        }

        // Deduplicated because a reason need not name its Force. Most do — "Settler is too small to split." — but a configuration error such as an
        // Unled organisation with no authored base size is a fact about the tag, so every Force sharing that tag reports it word for word.
        // Distinct preserves first-seen order, so the reasons stay in Force order.
        return ForceActionAvailability.Blocked([.. reasons.Distinct()]);
    }
}
