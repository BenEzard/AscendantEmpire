using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One Player's research project selection and carried-over points.
/// </summary>
/// <remarks>
/// <para>
/// Shares its primary key with <see cref="Player"/>, the established shape for a one-to-one extension here (as <c>UnitComponentBody</c> and
/// <c>CompiledUnitTemplate</c> do with their parents). These columns are deliberately <b>not</b> on <c>Player</c>, which stays thin - military organisation,
/// asset unlocks and design state all live in their own tables too.
/// </para>
/// <para>
/// A row is written for every Player at game setup rather than on first use: a lazily created row would force a null branch on every read and an upsert under
/// concurrency, for no gain when there is exactly one row per Player.
/// </para>
/// </remarks>
public class PlayerResearchState
{
    public int PlayerId { get; set; }

    /// <summary>
    /// The Game the Player belongs to.
    /// </summary>
    /// <remarks>Implied by <see cref="PlayerId"/>, but carried so the game delete can purge these rows directly rather than through the Player.</remarks>
    public int GameId { get; set; }

    /// <summary>
    /// The Technology directed research is currently going into, or <see langword="null"/> when none is selected.
    /// </summary>
    /// <remarks>Null is a real state, and the one the start-of-turn "select a research project" notification exists for.</remarks>
    public int? CurrentTechnologyId { get; set; }

    /// <summary>
    /// The Technology to switch to automatically when the current one completes, or <see langword="null"/> when none is pre-chosen.
    /// </summary>
    /// <remarks>
    /// Player intent, so it is stored. Whether the Player is <i>allowed</i> to set it is a different question and is derived - the Research Strategist
    /// specialist is what grants the ability - so no capability flag is cached here.
    /// </remarks>
    public int? NextTechnologyId { get; set; }

    /// <summary>
    /// Directed-research points generated but not yet spent.
    /// </summary>
    /// <remarks>
    /// The only genuinely fungible pool of the three. Whether these survive to next turn or are discarded is the game's
    /// <c>UnspentResearchPointsBehaviour</c> setting.
    /// </remarks>
    public int UnspentDirectedPoints { get; set; }

    /// <summary>
    /// Diffusion points that spilled out of a filled bucket and have not been used.
    /// </summary>
    /// <remarks>
    /// Asymmetric with <see cref="UnspentDirectedPoints"/>: diffusion points arrive attached to a trade partner or a neighbour rather than as a budget the
    /// Player spends freely. It stays a single scalar only because diffusion accrual is not modelled yet; it will likely become typed, as experience already
    /// is on <see cref="PlayerUnusedExperience"/>, once it is.
    /// </remarks>
    public int UnspentDiffusionPoints { get; set; }

    /// <summary>
    /// How many people it takes to produce one directed research point for this Player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The design doc's <c>TECH_POINTS_PER_POPULATION_SIZE</c>. Per Player rather than per Game because it is expected to move with a Player's own technology
    /// as the game runs, so it sits here beside the rest of their research state rather than beside the Game's research settings on
    /// <c>GameConfiguration</c>.
    /// </para>
    /// <para>
    /// A divisor, so it is constrained positive by <c>CK_PlayerResearchState_TechPointsPerPopulationSizePositive</c> rather than left to trip a divide by
    /// zero at the start of somebody's Turn.
    /// </para>
    /// </remarks>
    public int TechPointsPerPopulationSize { get; set; }

    /// <summary>
    /// The last Round the start-of-turn research generation ran for this Player, or <see langword="null"/> if it never has.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="LastResearchAppliedOnRound"/>, which guards the end-of-turn step. Two steps need two stamps: generation and spending happen
    /// at opposite ends of a Turn, and either may be retried without the other having run.
    /// </remarks>
    public int? LastResearchGeneratedOnRound { get; set; }

    /// <summary>
    /// The last Round the end-of-turn research step ran for this Player, or <see langword="null"/> if it never has.
    /// </summary>
    /// <remarks>
    /// A replay guard, in the style of <c>UnitInstance.CampaignMovementReplenishedOnRound</c>: the step distributes pending experience and spends points, and
    /// running it twice in one Round would double both.
    /// </remarks>
    public int? LastResearchAppliedOnRound { get; set; }

    public Player Player { get; set; } = null!;

    public Technology? CurrentTechnology { get; set; }

    public Technology? NextTechnology { get; set; }
}
