using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// The rule settings chosen for one game, as distinct from the map figures snapshotted onto <see cref="GameIndex"/>.
/// </summary>
/// <remarks>
/// <para>
/// Shares its primary key with <see cref="GameIndex"/>, and the row is created <b>unconditionally</b> during game setup, in the same save as the GameIndex row
/// itself. A lazily created settings row would mean every read is a null-coalesce to hard-coded defaults, and two games created a week apart behaving
/// differently with nothing in the data to explain why.
/// </para>
/// <para>
/// <b><see cref="GameIndex"/> keeps the settings it already has</b> - BattleMapSize, HorizontalWrap and VerticalWrap stay there. Game settings therefore live
/// in two places, so where a new one belongs is a decision to be asked about rather than assumed.
/// </para>
/// <para>
/// So far every column is a technology setting, because Technology is the first subsystem to need any. The table is named for what it is rather than for its
/// current contents.
/// </para>
/// </remarks>
public class GameConfiguration
{
    public int GameId { get; set; }

    /// <summary>
    /// The flat number of directed research points every Player generates per turn before modifiers.
    /// </summary>
    /// <remarks>The design doc's <c>EMPIRE_BASE_RESEARCH_POINTS</c>. Modifiers on top of it come from <c>PlayerResearchModifier</c>.</remarks>
    public int EmpireBaseResearchPoints { get; set; }

    /// <summary>
    /// Multiplier applied to every technology's research requirements, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100%).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 1,500 makes technologies take 50% longer. Applied by the requirement generator at setup, so it is baked into each Player's own figures rather than
    /// re-applied on read - changing it afterwards would have no effect on a game already set up.
    /// </para>
    /// <para>
    /// Scaled rather than a decimal because that is how this codebase stores every multiplier and percentage, and because the arithmetic has to come out
    /// bit-identical for every Player under <see cref="TechnologyCustomisation.Identical"/>.
    /// </para>
    /// </remarks>
    public long TechnologySpeedMultiplierPercent { get; set; }

    /// <summary>
    /// How far a technology's requirements may be randomly adjusted either way, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100%).
    /// </summary>
    /// <remarks>
    /// 5,000 means plus or minus 5%, shared equally across the buckets a technology uses. Read by the requirement generator at setup, which seeds its RNG from
    /// <see cref="GameIndex.GenerationSeed"/> so a game is reproducible; the fuzzed figure is written into the Player's row rather than the roll being stored.
    /// Ignored while only <see cref="TechnologyCustomisation.Identical"/> is implemented.
    /// </remarks>
    public long TechnologyFuzzinessPercent { get; set; }

    /// <summary>
    /// How much each Player's technology requirements differ from every other Player's.
    /// </summary>
    /// <remarks>Only <see cref="TechnologyCustomisation.Identical"/> is implemented; game creation rejects the other two.</remarks>
    public TechnologyCustomisation TechnologyCustomisation { get; set; }

    /// <summary>
    /// Whether points left over from finishing a technology may be spent on another one the same turn.
    /// </summary>
    /// <remarks>
    /// The game-level gate. Overflow also needs the Research Coordinator specialist, which is derived from the Player's Department rather than cached - so
    /// there is no per-Player overflow flag anywhere.
    /// </remarks>
    public bool ResearchOverflowEnabled { get; set; }

    /// <summary>
    /// Whether research points a Player could not spend are discarded or carried over.
    /// </summary>
    public UnspentResearchPointsBehaviour UnspentResearchPointsBehaviour { get; set; }

    public GameIndex Game { get; set; } = null!;
}
