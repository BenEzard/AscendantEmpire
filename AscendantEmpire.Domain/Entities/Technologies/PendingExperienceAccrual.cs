using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// Experience a Player has earned this turn, held until the end-of-turn step distributes it.
/// </summary>
/// <remarks>
/// <para>
/// Producers never allocate directly - they only ever add to this table, and the end-of-turn research step reads it, splits each type's total across the
/// technologies that consume it, and deletes the rows. The table is therefore <b>empty between turns</b>. Composite primary key on
/// (PlayerId, ExperienceTypeId).
/// </para>
/// <para>
/// <b>Collating matters for correctness, not just write volume.</b> An even split has to be computed against one settled set of recipients: allocating as each
/// event arrives means a Technology discovered halfway through a turn changes the divisor, so the same ten tile-steps through jungle would distribute
/// differently depending on the order things happened. One split per turn against one snapshot is deterministic and testable.
/// </para>
/// <para>
/// It is an accumulator, not an append-only event log - a Force crossing ten jungle tiles increments one row rather than inserting ten. Add with
/// <c>ExecuteUpdateAsync</c>, inserting only when no row exists. The cost is that per-event detail is gone before anything could report on it, which is
/// acceptable because <see cref="ResearchHistory"/> carries the per-turn totals.
/// </para>
/// </remarks>
public class PendingExperienceAccrual
{
    public int PlayerId { get; set; }

    public int ExperienceTypeId { get; set; }

    /// <summary>
    /// The Game the Player belongs to.
    /// </summary>
    /// <remarks>Implied by <see cref="PlayerId"/>, but carried so the game delete can purge these rows directly rather than through the Player.</remarks>
    public int GameId { get; set; }

    /// <summary>
    /// Points accrued so far this turn, summed across every action that produced this kind of experience.
    /// </summary>
    public int Points { get; set; }

    public Player Player { get; set; } = null!;

    public ExperienceType ExperienceType { get; set; } = null!;
}
