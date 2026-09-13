using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// Decides whether a Force is offered the Battle button, which opens the Battle it is fighting in.
/// </summary>
/// <remarks>
/// <para>
/// The simplest of the strategies: a Force in battle always has a Battle to look at, and a Force out of battle never does. There is no in-between state worth greying the button out for, so it
/// is either shown and enabled or not shown at all.
/// </para>
/// <para>
/// With several Forces selected the aggregator hides the button unless one of them is in battle, and disables it unless all of them are. That is the right reading for now - one dialog cannot
/// show several Battles at once - and it needs no special handling here.
/// </para>
/// </remarks>
internal sealed class BattleStrategy : IForceActionStrategy
{
    public ForceActionType ActionType => ForceActionType.Battle;

    public string Label => "Battle";

    public ForceActionAvailability? Evaluate(ForceActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Structural: a Force that is not fighting has no Battle to open, so the button is hidden rather than greyed out.
        if (!context.SelectedForce.IsInBattle)
        {
            return null;
        }

        return ForceActionAvailability.Available();
    }
}
