namespace AscendantEmpire.Domain.Enums.Games;

/// <summary>
/// How much each Player's technology requirements differ from every other Player's, for one game.
/// </summary>
/// <remarks>
/// <para>
/// Chosen once at game creation and read once, by the requirement generator, which writes each Player's own figures into their <c>PlayerTechnology</c> rows.
/// Because the result is materialised per Player, switching mode later would have no effect on a game already set up - it is a creation-time setting only.
/// </para>
/// <para>
/// <b>Only <see cref="Identical"/> is implemented.</b> The other two have no generator yet and game creation rejects them, so that a game can never be created
/// in a mode that silently behaves as another.
/// </para>
/// </remarks>
public enum TechnologyCustomisation
{
    /// <summary>
    /// Every Player faces the same requirements - the catalogue figures with only the speed multiplier applied.
    /// </summary>
    Identical = 1,

    /// <summary>
    /// Requirements are randomised per Player, then rebalanced per era so no Player's total is more than 1% from any other's. Not yet implemented.
    /// </summary>
    RandomisedEqual = 2,

    /// <summary>
    /// Requirements are randomised per Player with no rebalancing, so Players can be meaningfully better or worse off. Not yet implemented.
    /// </summary>
    FullyRandomised = 3
}
