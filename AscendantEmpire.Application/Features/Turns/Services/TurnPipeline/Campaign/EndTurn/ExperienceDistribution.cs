namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// One Player's accrued experience, as the technologies that consume it and the headroom each had when the drain began.
/// </summary>
/// <param name="TechnologyId">The technology that may receive points.</param>
/// <param name="Headroom">How many points its experience bucket can still take, which is its requirement less what it holds.</param>
/// <param name="ExperienceTypeIds">The experience types it consumes, from <c>TechnologyExperienceType</c>.</param>
internal sealed record ExperienceRecipient(int TechnologyId, int Headroom, IReadOnlySet<int> ExperienceTypeIds);

/// <summary>
/// Where one Turn's accrued experience went.
/// </summary>
/// <param name="PointsByTechnologyId">Points to add to each technology's experience bucket. Technologies receiving nothing are absent rather than zero.</param>
/// <param name="UnusedPointsByExperienceTypeId">Points that could not be used, per type, bound for <c>PlayerUnusedExperience</c>.</param>
internal sealed record ExperienceSplit(
    IReadOnlyDictionary<int, int> PointsByTechnologyId,
    IReadOnlyDictionary<int, int> UnusedPointsByExperienceTypeId);

/// <summary>
/// The rule for dividing one Player's accrued experience between the technologies that consume it.
/// </summary>
/// <remarks>
/// <para>
/// Kept as a pure static beside <see cref="ApplyResearch"/>, the way <see cref="ResearchBucketFill"/> is, so the whole table of cases can be pinned without
/// standing up a database and exactly one implementation of the rule runs in the tests and in production.
/// </para>
/// <para>
/// <b>Every point is accounted for.</b> What does not land in a bucket is reported as unused against the type that earned it - the remainder of an uneven
/// split, the surplus from a clamp, and the whole total of a type nothing consumes. A point that appeared in neither half of the result would be invisible to
/// every later report, which is the one failure this rule exists to prevent.
/// </para>
/// <para>
/// <b>Intended allocations are accumulated across every type before anything is clamped</b>, so a technology fed by two types is clamped once against the
/// headroom it had before the drain began. Clamping per type would make the outcome depend on which type was applied first, and the only available orders are
/// catalogue artefacts.
/// </para>
/// </remarks>
internal static class ExperienceDistribution
{
    /// <summary>
    /// Divides each type's points between the recipients that consume it, clamps each recipient once, and reports what could not be used.
    /// </summary>
    /// <param name="pointsByExperienceTypeId">What the Player accrued this Turn, per experience type. Entries of zero or less are ignored.</param>
    /// <param name="recipients">The settled recipient snapshot. A recipient with no headroom receives nothing and sends its whole share to unused.</param>
    public static ExperienceSplit Split(
        IReadOnlyDictionary<int, int> pointsByExperienceTypeId,
        IReadOnlyCollection<ExperienceRecipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(pointsByExperienceTypeId);
        ArgumentNullException.ThrowIfNull(recipients);

        Dictionary<int, int> unusedByTypeId = [];
        Dictionary<int, int> intendedByTechnologyId = [];

        // Which type gave how much to which technology, so a clamp can hand its surplus back to the types that caused it.
        Dictionary<int, Dictionary<int, int>> contributionsByTechnologyId = [];

        foreach ((int experienceTypeId, int points) in pointsByExperienceTypeId)
        {
            if (points <= 0)
            {
                continue;
            }

            ExperienceRecipient[] consumers = [.. recipients.Where(recipient => recipient.ExperienceTypeIds.Contains(experienceTypeId))];

            if (consumers.Length == 0)
            {
                AddTo(unusedByTypeId, experienceTypeId, points);
                continue;
            }

            int share = points / consumers.Length;

            AddTo(unusedByTypeId, experienceTypeId, points - (share * consumers.Length));

            if (share == 0)
            {
                continue;
            }

            foreach (ExperienceRecipient consumer in consumers)
            {
                AddTo(intendedByTechnologyId, consumer.TechnologyId, share);

                if (contributionsByTechnologyId.TryGetValue(consumer.TechnologyId, out Dictionary<int, int>? contributions) == false)
                {
                    contributions = [];
                    contributionsByTechnologyId[consumer.TechnologyId] = contributions;
                }

                AddTo(contributions, experienceTypeId, share);
            }
        }

        Dictionary<int, int> pointsByTechnologyId = [];

        foreach (ExperienceRecipient recipient in recipients)
        {
            if (intendedByTechnologyId.TryGetValue(recipient.TechnologyId, out int intended) == false)
            {
                continue;
            }

            int allocated = Math.Min(intended, Math.Max(0, recipient.Headroom));

            if (allocated > 0)
            {
                pointsByTechnologyId[recipient.TechnologyId] = allocated;
            }

            ReturnSurplus(unusedByTypeId, contributionsByTechnologyId[recipient.TechnologyId], intended - allocated);
        }

        return new ExperienceSplit(pointsByTechnologyId, unusedByTypeId);
    }

    /// <summary>
    /// Hands a clamped recipient's surplus back to the types that fed it, in ascending type id.
    /// </summary>
    /// <remarks>
    /// A fixed order rather than a proportional share: proportional attribution needs its own rounding rule, and ascending id is reproducible without one.
    /// It only ever matters for a technology that consumes more than one type, which no seeded technology currently does.
    /// </remarks>
    private static void ReturnSurplus(Dictionary<int, int> unusedByTypeId, Dictionary<int, int> contributions, int surplus)
    {
        foreach (int experienceTypeId in contributions.Keys.Order())
        {
            if (surplus <= 0)
            {
                return;
            }

            int returned = Math.Min(surplus, contributions[experienceTypeId]);

            AddTo(unusedByTypeId, experienceTypeId, returned);

            surplus -= returned;
        }
    }

    /// <summary>
    /// Adds to a running total, leaving the dictionary untouched where there is nothing to add.
    /// </summary>
    private static void AddTo(Dictionary<int, int> totals, int key, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        totals[key] = totals.TryGetValue(key, out int existing) ? existing + amount : amount;
    }
}
