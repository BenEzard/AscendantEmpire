using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One modifier to how many research points of one bucket a Player generates per turn, granted by one source.
/// </summary>
/// <remarks>
/// <para>
/// The design doc's formula is <c>EMPIRE_BASE_RESEARCH_POINTS + …</c>, and this is where the rest of it lives - the Technology, Specialist or Department that
/// boosts research. Modelled on <c>PlayerSustenanceModifier</c>, deliberately and exactly.
/// </para>
/// <para>
/// A Player's effective generation is never stored. It is derived from the game's base figure and the modifiers the Player currently holds:
/// <c>Base + Base × ΣPercentValue + ΣFlatValue</c>. Sources can be lost again - a Specialist dies, a Department is dissolved - and deriving the value means
/// losing one is just deleting its rows by (<see cref="SourceType"/>, <see cref="SourceId"/>), with no running total to unwind.
/// </para>
/// <para>
/// No navigation properties beyond the Player: the rows are summed, never walked as an entity graph.
/// </para>
/// </remarks>
public class PlayerResearchModifier
{
    /// <summary>
    /// Surrogate key.
    /// </summary>
    /// <remarks>
    /// Uniqueness is enforced by the <c>UX_PlayerResearchModifier_Player_Source_Bucket</c> index rather than by the key, matching
    /// <c>PlayerSustenanceModifier</c>.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// The Game the Player belongs to.
    /// </summary>
    /// <remarks>Implied by <see cref="PlayerId"/>, but carried so the game delete can purge these rows directly rather than through the Player.</remarks>
    public int GameId { get; set; }

    /// <summary>
    /// The Player whose research generation is modified.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// What kind of thing granted this modifier.
    /// </summary>
    /// <remarks>Reuses the existing <see cref="ModifierSourceType"/> rather than introducing a parallel enum.</remarks>
    public ModifierSourceType SourceType { get; set; }

    /// <summary>
    /// The particular Technology, Specialist or Department that granted this modifier, according to <see cref="SourceType"/>.
    /// </summary>
    /// <remarks>
    /// Not a foreign key: it points at a different table for each source type. Technology is now modelled, so a <c>Technology</c> source is a real id even
    /// though the column cannot constrain it; Specialists and Departments still are not.
    /// </remarks>
    public int SourceId { get; set; }

    /// <summary>
    /// Which of the three research buckets is modified.
    /// </summary>
    public ResearchBucket Bucket { get; set; }

    /// <summary>
    /// A flat amount added to the bucket's generation, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Negative for a penalty; 0 when this modifier has no
    /// flat part.
    /// </summary>
    public long FlatValue { get; set; }

    /// <summary>
    /// A percentage of the base added to the bucket's generation, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100%). Negative for a penalty; 0 when
    /// this modifier has no percentage part.
    /// </summary>
    public long PercentValue { get; set; }

    public Player Player { get; set; } = null!;
}
