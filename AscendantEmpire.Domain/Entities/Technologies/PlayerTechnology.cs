using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One Player's independent progress toward one Technology.
/// </summary>
/// <remarks>
/// <para>
/// <b>A row exists for every Player and every Technology from game setup onwards, so presence does NOT mean unlocked.</b> This deliberately departs from
/// <c>PlayerUnlockedAsset</c>, one feature away, where the presence of a row is itself the unlock - an availability check must not be copied across. Here the
/// question is always <see cref="Status"/>. Composite primary key on (PlayerId, TechnologyId).
/// </para>
/// <para>
/// Rows exist up front because the requirement figures are materialised per Player: <c>RandomisedEqual</c> has to balance each Player's totals against every
/// other's, per era, which can only be done with the whole set in hand. Under <c>Identical</c> every Player's figures match, which is the correct answer rather
/// than a placeholder.
/// </para>
/// <para>
/// <b><see cref="Status"/> is a materialised cache.</b> Locked versus Available is a pure function of the prerequisite edges and the Player's discovered set,
/// recomputed by one owner - the availability reconcile - after any discovery, at start of turn, and after any seed that touched the catalogue. That last
/// trigger is the one that matters: seeding a new Technology leaves every Player in every existing game with no row for it at all, so the reconcile has to
/// backfill rows before it recomputes statuses. Because the recompute is idempotent, a race on it is harmless. Do not "optimise" it away.
/// </para>
/// <para>
/// Rounds, not Turns: the game clock is <c>TurnQueue.CurrentRoundNumber</c>, and there is no current turn number to compare a Turn against.
/// </para>
/// </remarks>
public class PlayerTechnology
{
    public int PlayerId { get; set; }

    public int TechnologyId { get; set; }

    /// <summary>
    /// The Game the Player belongs to.
    /// </summary>
    /// <remarks>
    /// Implied by <see cref="PlayerId"/>, but carried so the game delete can purge these rows directly rather than through the Player - and so that the
    /// diffusion question "which other Players in this game have discovered X?" has an index that does not span every game in the database.
    /// </remarks>
    public int GameId { get; set; }

    /// <summary>
    /// How far this Player has got. The authoritative, indexed state.
    /// </summary>
    public TechnologyStatus Status { get; set; }

    /// <summary>
    /// This Player's directed-research requirement, or <see langword="null"/> when this Technology does not use the directed bucket.
    /// </summary>
    /// <remarks>
    /// Generated once at game setup from the catalogue figure and the game's speed multiplier. Null exactly where the catalogue's is null: which buckets a
    /// Technology uses is never randomised. Completion is tested against this column, never against <c>Technology.DirectedRequired</c>.
    /// </remarks>
    public int? DirectedRequired { get; set; }

    /// <summary>
    /// This Player's experience requirement, or <see langword="null"/> when this Technology does not use the experience bucket.
    /// </summary>
    public int? ExperienceRequired { get; set; }

    /// <summary>
    /// This Player's diffusion requirement, or <see langword="null"/> when this Technology does not use the diffusion bucket.
    /// </summary>
    public int? DiffusionRequired { get; set; }

    /// <summary>
    /// Directed-research points accumulated so far, clamped at <see cref="DirectedRequired"/>.
    /// </summary>
    /// <remarks>
    /// Clamped on write rather than allowed to exceed the requirement, because the overflow calculation works from what remains and a negative remainder breaks
    /// it. Increment with <c>ExecuteUpdateAsync</c>, never read-modify-write.
    /// </remarks>
    public int DirectedPoints { get; set; }

    /// <summary>
    /// Experience points accumulated so far, clamped at <see cref="ExperienceRequired"/>.
    /// </summary>
    public int ExperiencePoints { get; set; }

    /// <summary>
    /// Diffusion points accumulated so far, clamped at <see cref="DiffusionRequired"/>.
    /// </summary>
    public int DiffusionPoints { get; set; }

    /// <summary>
    /// The Round the acquisition cost was settled, or <see langword="null"/> while it has not been.
    /// </summary>
    /// <remarks>
    /// <b>This is the unlock event.</b> The design doc defines unlocking as "all prerequisites discovered and the initial cost paid", and
    /// prerequisites-discovered is exactly what <see cref="TechnologyStatus.Available"/> already means - so there is no separate UnlockedOnRound, which would
    /// only be a second column for one event, free to drift from this one. A Technology with no acquisition cost rows still stamps the Round it unlocked: the
    /// cost was zero, not unpaid.
    /// </remarks>
    public int? AcquisitionPaidOnRound { get; set; }

    /// <summary>
    /// The Round every required bucket became full, or <see langword="null"/> while the Technology is unfinished.
    /// </summary>
    public int? DiscoveredOnRound { get; set; }

    /// <summary>
    /// How the Technology was finished, or <see langword="null"/> while it is unfinished.
    /// </summary>
    /// <remarks>How it was <i>finished</i>, not where all its points came from - see <see cref="TechnologyDiscoveryMethod"/>.</remarks>
    public TechnologyDiscoveryMethod? DiscoveryMethod { get; set; }

    /// <summary>
    /// The Player it was taken or received from, where the method had a counterparty.
    /// </summary>
    /// <remarks>
    /// Trade, espionage, battlefield capture and conquest all have a source, and <see cref="DiscoveryMethod"/> records only the kind. Carried from the start
    /// because adding it later would be a migration over live rows.
    /// </remarks>
    public int? DiscoveredFromPlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public Technology Technology { get; set; } = null!;
}
