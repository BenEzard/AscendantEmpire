using AscendantEmpire.Domain.Entities.Technologies;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// The rule for pouring a pool of directed research points into one Technology's three buckets.
/// </summary>
/// <remarks>
/// <para>
/// Kept as a pure static beside <see cref="ApplyResearch"/>, the way <c>MovementReplenishment</c> sits beside <c>ReplenishMovementPoints</c>, so the whole
/// table of cases can be pinned without standing up a database and exactly one implementation of the rule runs in the tests and in production.
/// </para>
/// <para>
/// <b>Directed research fills every bucket, not only its own.</b> The design is explicit that directed research "can <i>fully</i> complete a technology
/// without experience or diffusion", so a pool that finishes the directed bucket carries on into experience and then diffusion of the <i>same</i>
/// Technology. Carrying what is left onto a <i>different</i> Technology is overflow, which is a separate rule and is not done here.
/// </para>
/// <para>
/// Directed is filled first because it is the bucket the research project exists to fill: directed points stand in for the passive buckets only once the
/// bucket they belong to is complete. Experience before diffusion is the order the three are declared in everywhere else in the model, and nothing yet
/// distinguishes them.
/// </para>
/// </remarks>
internal static class ResearchBucketFill
{
    /// <summary>
    /// Pours a pool of points into a Technology's buckets, directed first, and reports how many landed.
    /// </summary>
    /// <param name="playerTechnology">The Player's row for the Technology being researched. Its three point columns are written.</param>
    /// <param name="availablePoints">The pool to spend. Zero or negative spends nothing.</param>
    /// <returns>How many points went into a bucket. The caller's pool less this is what had nowhere to go.</returns>
    /// <remarks>
    /// Every bucket is left clamped at its requirement, which is what <c>PlayerTechnology</c>'s own remarks demand and what keeps the overflow calculation's
    /// "what remains" from going negative.
    /// </remarks>
    public static int Spend(PlayerTechnology playerTechnology, int availablePoints)
    {
        int remainingPoints = Math.Max(0, availablePoints);

        int intoDirected = FillBucket(playerTechnology.DirectedRequired, playerTechnology.DirectedPoints, remainingPoints);
        playerTechnology.DirectedPoints += intoDirected;
        remainingPoints -= intoDirected;

        int intoExperience = FillBucket(playerTechnology.ExperienceRequired, playerTechnology.ExperiencePoints, remainingPoints);
        playerTechnology.ExperiencePoints += intoExperience;
        remainingPoints -= intoExperience;

        int intoDiffusion = FillBucket(playerTechnology.DiffusionRequired, playerTechnology.DiffusionPoints, remainingPoints);
        playerTechnology.DiffusionPoints += intoDiffusion;

        return intoDirected + intoExperience + intoDiffusion;
    }

    /// <summary>
    /// Whether every bucket this Technology actually uses is full, which is what discovery turns on.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> requirement is a bucket the Technology does not use at all, rather than one requiring zero, so it is satisfied by
    /// definition. The comparison is <c>&gt;=</c> rather than <c>==</c> so a row that somehow holds more than it needs still reads as complete.
    /// </remarks>
    public static bool IsComplete(PlayerTechnology playerTechnology)
    {
        return IsBucketFull(playerTechnology.DirectedRequired, playerTechnology.DirectedPoints)
            && IsBucketFull(playerTechnology.ExperienceRequired, playerTechnology.ExperiencePoints)
            && IsBucketFull(playerTechnology.DiffusionRequired, playerTechnology.DiffusionPoints);
    }

    /// <summary>
    /// How much of the pool one bucket can take before it is full.
    /// </summary>
    /// <remarks>An unused bucket takes nothing, and an already-full bucket takes nothing rather than a negative amount.</remarks>
    private static int FillBucket(int? required, int currentPoints, int availablePoints)
    {
        if (required is null)
        {
            return 0;
        }

        int headroom = Math.Max(0, required.Value - currentPoints);

        return Math.Min(availablePoints, headroom);
    }

    /// <summary>
    /// Whether one bucket has everything it needs, an unused bucket counting as satisfied.
    /// </summary>
    private static bool IsBucketFull(int? required, int currentPoints)
    {
        return required is null || currentPoints >= required.Value;
    }
}
