using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// What happened to one Player's research points in one bucket on one Round.
/// </summary>
/// <remarks>
/// <para>
/// A per-turn summary, three rows per Player per Round - deliberately not a per-event ledger, which would be unbounded in the number of actions a turn can
/// contain. It answers "why did I get 300 points this turn", which the Player will ask, and it is the input the research-project conversions will read.
/// </para>
/// <para>
/// Append-only, so it carries no RowVersion, matching <c>MovementHistory</c>, <c>StructureHistory</c> and <c>BattleHistory</c>.
/// </para>
/// </remarks>
public class ResearchHistory
{
    public int Id { get; set; }

    /// <summary>
    /// The Game the Player belongs to.
    /// </summary>
    /// <remarks>Implied by <see cref="PlayerId"/>, but carried so the game delete can purge these rows directly rather than through the Player.</remarks>
    public int GameId { get; set; }

    public int PlayerId { get; set; }

    /// <summary>
    /// The Turn this summary was written on, or <see langword="null"/> where it was not written from inside a Turn.
    /// </summary>
    /// <remarks>Null covers game setup and any maintenance recompute, neither of which happens inside a Player's Turn.</remarks>
    public int? PlayerTurnId { get; set; }

    /// <summary>
    /// The Round this summary covers.
    /// </summary>
    public int RoundNumber { get; set; }

    /// <summary>
    /// Which of the three buckets this row is about.
    /// </summary>
    public ResearchBucket Bucket { get; set; }

    /// <summary>
    /// Points produced this Round, before anything was spent.
    /// </summary>
    public int Generated { get; set; }

    /// <summary>
    /// Points that went into a Technology.
    /// </summary>
    public int Spent { get; set; }

    /// <summary>
    /// Points that finished one Technology and were carried onto another.
    /// </summary>
    public int Overflowed { get; set; }

    /// <summary>
    /// Points that could not be used and were not carried over.
    /// </summary>
    /// <remarks>
    /// For the experience bucket this is what landed on <see cref="PlayerUnusedExperience"/>; for the other two it is what the game's
    /// <c>UnspentResearchPointsBehaviour</c> setting discarded.
    /// </remarks>
    public int Lost { get; set; }

    public Player Player { get; set; } = null!;

    public PlayerTurn? PlayerTurn { get; set; }
}
