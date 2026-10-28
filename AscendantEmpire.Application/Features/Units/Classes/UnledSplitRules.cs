using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.Units.Classes;

/// <summary>
/// The unled rules applied when a Force is divided: what shapes a split may take, and how the Force's wounded travel with the pieces.
/// </summary>
/// <remarks>
/// <para>
/// The sizing rules come from <c>docs/design/units/military-organisation.md</c>, "Splitting and merging": every resulting group is at least one quantum
/// (the tag's <c>UnledBaseSize</c>), all but one are exact multiples of it, and one group carries whatever is left over. A lone part-quantum group never
/// exists — the remainder always rides with a whole group.
/// </para>
/// <para>
/// Sits beside <see cref="UnledSpawnRules"/> and takes the same stance on bad catalogue data: an unled tag authored without its sizes is reported as a
/// configuration error rather than quietly letting the guardrail through.
/// </para>
/// <para>
/// Pure, and deliberately so. The split handler owns loading and mutating; this owns the arithmetic, which is what makes it testable without a database
/// and what lets the API and the dialog agree on what a legal split looks like.
/// </para>
/// </remarks>
internal static class UnledSplitRules
{
    /// <summary>The smallest number of pieces a split can produce — anything less is not a split.</summary>
    /// <remarks>
    /// Shared with <c>SplitForceStrategy</c>, which gates the toolbar button on a Force being big enough to yield this many whole quanta. The two have to
    /// agree, or the button offers a split this class then refuses, so the number lives in one place.
    /// </remarks>
    public const int MINIMUM_DIVISIONS = 2;

    /// <summary>
    /// Returns every problem with dividing a Force of <paramref name="totalStrength"/> into <paramref name="groupSizes"/>, or an empty list when the
    /// split is legal (or no unled rule applies).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every broken rule is collected rather than returning at the first, so fixing one problem does not simply reveal the next on the following request.
    /// The one exception is a missing base size, which returns immediately: with no quantum there is nothing to measure the other rules against.
    /// </para>
    /// <para>
    /// The tag's <c>UnledMaxSize</c> is deliberately not checked. A split only ever shrinks a Force, so it cannot breach a ceiling the Force already sat
    /// under; <c>Max</c> is merging's constraint.
    /// </para>
    /// </remarks>
    /// <param name="organisationTag">The Force's organisation tag. A commissioned or absent tag imposes no unled splitting rule.</param>
    /// <param name="totalStrength">The Force's current strength — healthy and injured together, since the wounded travel with their group.</param>
    public static List<string> ValidateGroupSizes(Tag? organisationTag, int totalStrength, IReadOnlyCollection<int> groupSizes)
    {
        ArgumentNullException.ThrowIfNull(groupSizes);

        List<string> errors = [];

        // No organisation tag, or a commissioned one, imposes no unled splitting rule. A commissioned formation detaches along tier lines instead, which
        // is a different mechanic with different rules.
        if (organisationTag is not { LeadershipModel: LeadershipModel.Unled })
        {
            return errors;
        }

        // An unled tag is authored with its sizes together, and tag-save time enforces that. Reaching here without them is a configuration error, so say
        // so plainly instead of quietly letting the size guardrail through.
        if (organisationTag.UnledBaseSize is not > 0)
        {
            errors.Add($"Organisation '{organisationTag.DisplayName}' is Unled but has no authored base size, so its forces cannot be split.");

            return errors;
        }

        int baseSize = organisationTag.UnledBaseSize.Value;

        // One group is the Force as it already stands, so it is not a split at all.
        if (groupSizes.Count < MINIMUM_DIVISIONS)
        {
            errors.Add($"A split must produce at least {MINIMUM_DIVISIONS} groups (got {groupSizes.Count}).");
        }

        // The floor is one whole quantum. This is the rule that rules out the lone part-group: a remainder small enough to sit below the quantum has to be
        // carried by a group that is already whole, not stood up as a group of its own.
        List<int> undersizedGroups = [.. groupSizes.Where(size => size < baseSize)];

        if (undersizedGroups.Count > 0)
        {
            errors.Add($"Every group must be at least {baseSize} strong (got {string.Join(", ", undersizedGroups)}).");
        }

        // A split neither invents nor loses soldiers.
        int requestedStrength = groupSizes.Sum();

        if (requestedStrength != totalStrength)
        {
            errors.Add($"The groups must account for all {totalStrength} soldiers in the force (got {requestedStrength}).");
        }

        // At most one group may be ragged. Note what this check does *not* need to do: because the sizes are already required to sum to the Force's
        // strength, and every clean multiple is divisible by the quantum, the single ragged group's leftover is forced to equal the Force's own remainder.
        // So there is no separate "the remainder is the right size" rule to write — the arithmetic settles it.
        int raggedGroupCount = groupSizes.Count(size => size % baseSize != 0);

        if (raggedGroupCount > 1)
        {
            errors.Add($"Only one group may carry the remainder; the rest must be whole multiples of {baseSize} ({raggedGroupCount} are not).");
        }

        return errors;
    }

    /// <summary>
    /// Divides a Force's injured across the groups a split produces, in proportion to each group's size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Proportional is the neutral choice: nothing in the design concentrates the wounded anywhere, and it needs no extra decision from the player.
    /// The server owns it rather than taking casualty counts from the client, which would let a player sweep every casualty into one group and disband it.
    /// </para>
    /// <para>
    /// Rounding is settled by largest remainder, so the parts sum back to exactly <paramref name="totalInjuredCount"/> — no soldier is invented or lost by
    /// the split. Ties fall to the earlier group, which makes the result deterministic and matches the dialog's preview soldier for soldier.
    /// </para>
    /// </remarks>
    /// <returns>One injured count per group, in the order the groups were given.</returns>
    public static List<int> ApportionInjured(IReadOnlyCollection<int> groupSizes, int totalInjuredCount)
    {
        ArgumentNullException.ThrowIfNull(groupSizes);

        int apportionableStrength = groupSizes.Sum();

        // Nothing to hand out, or nothing to hand it out across. Returning a zero per group keeps the result the same shape as the input, so callers never
        // have to special-case an unwounded Force.
        if (apportionableStrength <= 0 || totalInjuredCount <= 0)
        {
            return [.. groupSizes.Select(_unused => 0)];
        }

        List<double> exactShares = [.. groupSizes.Select(size => (double)totalInjuredCount * size / apportionableStrength)];

        // Each group's share rounded down. That always leaves something over unless every share was whole, which is what the pass below hands out.
        List<int> injuredCounts = [.. exactShares.Select(share => (int)Math.Floor(share))];

        int soldiersToDistribute = totalInjuredCount - injuredCounts.Sum();

        // What rounding down threw away, given back one soldier at a time to whichever group lost the most to it.
        List<int> groupsByDiscardedFraction =
        [
            .. exactShares
                .Select((share, index) => (Index: index, DiscardedFraction: share - Math.Floor(share)))
                .OrderByDescending(group => group.DiscardedFraction)
                .ThenBy(group => group.Index)
                .Select(group => group.Index)
        ];

        foreach (int groupIndex in groupsByDiscardedFraction)
        {
            if (soldiersToDistribute <= 0)
            {
                break;
            }

            injuredCounts[groupIndex]++;
            soldiersToDistribute--;
        }

        return injuredCounts;
    }
}
