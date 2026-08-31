using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// Decides whether a troop can be divided into two or more smaller troops.
/// </summary>
/// <remarks>
/// <para>
/// Only unled formations split this way. An unled troop has no officer holding it together, so it is sized purely in whole multiples of its
/// organisation tag's base quantum and may be divided along those lines. A commissioned formation would instead detach a sub-formation under a
/// subordinate officer, which is a different action with different rules — hence this strategy hides itself rather than offering a disabled
/// button for troops it can never apply to.
/// </para>
/// <para>Splitting costs the troop its entire turn, so it requires a troop that has not yet moved.</para>
/// </remarks>
internal sealed class SplitForceStrategy : IForceActionStrategy
{
    /// <summary>The smallest number of pieces a split can produce — anything less is not a split.</summary>
    private const int _MINIMUM_DIVISIONS = 2;

    public ForceActionType ActionType => ForceActionType.SplitForce;

    public string Label => "Split Force";

    public ForceActionAvailability? Evaluate(ForceActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ForceActionForce troop = context.SelectedForce;

        // Structural: a troop whose template carries no organisation tag, or a commissioned one, can never split this way.
        // Hidden rather than disabled, because no amount of reinforcing or resting will change it.
        if (troop.Organisation is not { IsUnled: true } organisation)
        {
            return null;
        }

        List<string> reasons = [];

        // An unled tag is authored with its base and max sizes together, and tag-save time enforces that.
        // Reaching here without them is a configuration error, so say so plainly instead of quietly letting the size guardrail through — the same stance UnledSpawnRules takes.
        if (organisation.UnledBaseSize is not > 0)
        {
            reasons.Add($"Organisation '{organisation.DisplayName}' is Unled but has no authored base size, so its troops cannot be split.");

            return ForceActionAvailability.Blocked(reasons);
        }

        int baseSize = organisation.UnledBaseSize.Value;
        int smallestSplittableSize = baseSize * _MINIMUM_DIVISIONS;

        // The troop must be able to yield at least two whole groups; a troop of one group has nothing to divide into.
        if (troop.TotalStrength < smallestSplittableSize)
        {
            reasons.Add($"{troop.DisplayName} is too small to split.");
        }

        // Splitting consumes the whole turn, so it needs a troop that has not yet spent any of its movement — not merely one with some left.
        if (!troop.HasFullMovement)
        {
            reasons.Add($"{troop.DisplayName} has already moved this turn. Splitting requires a full turn.");
        }

        return ForceActionAvailability.Blocked(reasons);
    }
}
