namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// What a Player knows of one MapResourceType as a whole: when they first saw one, and when they researched it. Keyed on the Player and the type.
/// </summary>
/// <remarks>
/// <para>
/// First sight of a type adds a "Research X" item to the Player's tech list. Researching it makes them far more likely to discover other instances of that type, and, for a
/// type that <c>RequiresResearchToExtract</c>, is what lets them extract it at all. See "Discovery" in <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// Stands in for the research state until Technology is modelled, in the style of <c>PlayerUnlockedComponent</c>: no row means the Player has never seen the type.
/// </para>
/// </remarks>
public class PlayerMapResourceTypeKnowledge
{
    public int PlayerId { get; set; }

    /// <summary>
    /// The type known about (a <c>map.MapResourceType</c>).
    /// </summary>
    public int MapResourceTypeId { get; set; }

    /// <summary>
    /// The Game the Player belongs to, carried so the game delete can purge these rows directly.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// The Round the Player first saw an instance of the type.
    /// </summary>
    public int FirstSeenOnRound { get; set; }

    /// <summary>
    /// The Round the Player finished researching the type, or <see langword="null"/> while they have not.
    /// </summary>
    public int? ResearchedOnRound { get; set; }
}
