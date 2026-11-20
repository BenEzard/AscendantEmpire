using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// Decides whether a Force can take a MapResource off the tile it stands on.
/// </summary>
/// <remarks>
/// <para>
/// What is takeable is the context builder's answer, not this strategy's: whether an instance holds a quantity of its own, whether the Player has
/// discovered it, whether its type needs researching first and whether any of its yields survives their switches are all database questions, and a strategy
/// must never ask one. By the time a resource reaches <see cref="ForceActionContext.TakeableMapResourcesOnTile"/> it is takeable, so all that is left here
/// is whether this Force is in a fit state to take it.
/// </para>
/// <para>
/// <b>Which resource is not decided here.</b> A tile holds up to two, and the client reads the options endpoint to find out how many: one is taken
/// outright, two are offered to the player to choose between. The button can only say whether an order is possible, never which one the player meant.
/// </para>
/// </remarks>
internal sealed class TakeResourcesStrategy : IForceActionStrategy
{
    public ForceActionType ActionType => ForceActionType.TakeResources;

    public string Label => "Take Resources";

    public ForceActionAvailability? Evaluate(ForceActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ForceActionForce force = context.SelectedForce;

        // Structural: a Force fighting a battle is not foraging through it, and the same tile is what refuses it a campaign step.
        if (force.IsInBattle)
        {
            return null;
        }

        // Structural too, and the reason the button is not simply greyed out everywhere: most tiles hold nothing takeable, and a toolbar that explained
        // that on every one of them would be a wall of disabled buttons. Nothing the Force does this turn can put a resource under its feet.
        if (context.TakeableMapResourcesOnTile.Count == 0)
        {
            return null;
        }

        List<string> reasons = [];

        // The wounded carry nothing, so a Force of nothing but casualties has no one to load up - see TakeResourcesRules.
        if (force.HealthyCount <= 0)
        {
            reasons.Add($"{force.DisplayName} has no healthy individuals to carry anything.");
        }

        // A full Turn, not merely some movement left: loading up is the Force's whole Turn, the way a split is. Note what this does and does not prove -
        // unspent movement carries forward since F-14, so a Force at or above its allowance may still have moved. See ForceActionForce.HasFullMovement.
        if (!force.HasFullMovement)
        {
            reasons.Add($"{force.DisplayName} has already moved this turn. Taking resources costs a full turn.");
        }

        // Blocked with no reasons falls back to Available, so a clean pass needs no separate branch.
        return ForceActionAvailability.Blocked(reasons);
    }
}
