namespace AscendantEmpire.Domain.Enums.Turns;

public enum EndTurnMode
{
    /// <summary>
    /// The first time the Player requests to end their turn, the game will check if they have any pending actions that 
    /// they might want to resolve before the Turn ends. 
    /// </summary>
    /// <remarks>
    /// If there are pending actions, the game will prompt the Player to resolve them. 
    /// If there are no pending actions, the turn will end immediately.
    /// </remarks>
    CheckForActions,

    /// <summary>
    /// The Player want the turn to end, regardless of any pending actions.
    /// </summary>
    ForceEnd,

    /// <summary>
    /// Developer mode. It allows the Player to end their turn and skip all other Players turns until it is this Player's turn again.
    /// </summary>
    /// <remarks>
    /// This mode is used for testing and debugging purposes. 
    /// </remarks>
    Developer_AllAboutMe,
}
