namespace AscendantEmpire.Domain.Enums.Games;

/// <summary>
/// What happens to research points a Player generated but could not spend this turn.
/// </summary>
/// <remarks>
/// Points go unspent when no research project is selected, when nothing is unlocked to spend them on, or when overflow is disabled and the current project
/// needed fewer points than were available. Segmented by bucket: each of the three is decided separately.
/// </remarks>
public enum UnspentResearchPointsBehaviour
{
    /// <summary>
    /// The points are discarded at the end of the turn.
    /// </summary>
    Lost = 1,

    /// <summary>
    /// The points are carried over and remain available next turn.
    /// </summary>
    Returned = 2
}
