using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// Decides whether one kind of context action is offered for a selected Force, and whether it can be taken right now.
/// </summary>
/// <remarks>
/// <para>
/// One implementation per <see cref="ForceActionType"/>. Implementations are pure and synchronous: everything they need arrives on the
/// <see cref="ForceActionContext"/>, which the pipeline builds once per request, so a strategy must never query the database.
/// </para>
/// <para>
/// The same strategy instance serves <b>both</b> the read and the write. The toolbar endpoint calls <see cref="Evaluate"/> to render
/// availability, and the handler that executes the action calls it again against a freshly built context before doing anything. Keeping one
/// implementation of the rules is what stops the two drifting apart as rules change.
/// </para>
/// </remarks>
internal interface IForceActionStrategy
{
    /// <summary>The action this strategy decides. Its numeric value also fixes the action's place in the toolbar.</summary>
    ForceActionType ActionType { get; }

    /// <summary>
    /// The label shown on the button, and the fallback the frontend renders when it does not yet recognise <see cref="ActionType"/>.
    /// </summary>
    string Label { get; }

    /// <summary>
    /// Judge the action against the selected Force's current situation.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> when the action is <b>structurally irrelevant</b> to this Force and should be left out of the toolbar entirely —
    /// something that could never become true for it, such as splitting a Force whose organisation is commissioned rather than unled.
    /// Otherwise an availability describing whether it can be taken, and if not, why not.
    /// </returns>
    /// <remarks>
    /// The distinction is roughly "could this ever be true for this Force?". Structural facts hide the action; mutable state — size, movement,
    /// what else stands on the tile — disables it with an explanation. Without that split, every Force's toolbar would fill with greyed-out
    /// buttons for actions it can never have.
    /// </remarks>
    ForceActionAvailability? Evaluate(ForceActionContext context);
}
