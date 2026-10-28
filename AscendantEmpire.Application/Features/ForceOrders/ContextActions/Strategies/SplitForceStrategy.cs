using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// Decides whether a Force can be divided into two or more smaller Forces.
/// </summary>
/// <remarks>
/// <para>
/// Only unled formations split this way. An unled Force has no officer holding it together, so it is sized purely in whole multiples of its
/// organisation tag's base quantum and may be divided along those lines. A commissioned formation would instead detach a sub-formation under a
/// subordinate officer, which is a different action with different rules — hence this strategy hides itself rather than offering a disabled
/// button for Forces it can never apply to.
/// </para>
/// <para>Splitting costs the Force its entire turn, so it requires a Force that has not yet moved.</para>
/// </remarks>
internal sealed class SplitForceStrategy : IForceActionStrategy
{
    public ForceActionType ActionType => ForceActionType.SplitForce;

    public string Label => "Split Force";

    public ForceActionAvailability? Evaluate(ForceActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ForceActionForce force = context.SelectedForce;

        // Structural: a Force whose template carries no organisation tag, or a commissioned one, can never split this way.
        // Hidden rather than disabled, because no amount of reinforcing or resting will change it.
        if (force.Organisation is not { IsUnled: true } organisation)
        {
            return null;
        }

        List<string> reasons = [];

        // An unled tag is authored with its base and max sizes together, and tag-save time enforces that.
        // Reaching here without them is a configuration error, so say so plainly instead of quietly letting the size guardrail through — the same stance UnledSpawnRules takes.
        if (organisation.UnledBaseSize is not > 0)
        {
            reasons.Add($"Organisation '{organisation.DisplayName}' is Unled but has no authored base size, so its forces cannot be split.");

            return ForceActionAvailability.Blocked(reasons);
        }

        // The quantum count and the division rules are one thing, so the minimum lives with the rules the write path enforces rather than in a second copy
        // here. A button that offered a split UnledSplitRules then refused would be the worst of both.
        int baseSize = organisation.UnledBaseSize.Value;
        int smallestSplittableSize = baseSize * UnledSplitRules.MINIMUM_DIVISIONS;

        // The Force must be able to yield at least two whole groups; a Force of one group has nothing to divide into.
        if (force.TotalStrength < smallestSplittableSize)
        {
            reasons.Add($"{force.DisplayName} is too small to split.");
        }

        // Splitting consumes the whole turn, so it needs a Force that has not yet spent any of its movement — not merely one with some left.
        if (!force.HasFullMovement)
        {
            reasons.Add($"{force.DisplayName} has already moved this turn. Splitting requires a full turn.");
        }

        return ForceActionAvailability.Blocked(reasons);
    }
}
