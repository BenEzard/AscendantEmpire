namespace AscendantEmpire.Domain.Enums.Games;

public enum TurnStatus
{
    /// <summary>
    /// The game is still in setup phase.
    /// </summary>
    InSetup,

    /// <summary>
    /// The Player is awaiting their turn.
    /// </summary>
    Pending,

    /// <summary>
    /// The Player is currently taking their turn.
    /// </summary>
    TakingTurn,

    /// <summary>
    /// The turn has been taken and Completed in a normal fashion.
    /// </summary>
    TurnCompleted,

    /// <summary>
    /// The Battle has been Completed, so the Player doesn't need to have a turn.
    /// </summary>
    /// <remarks>For Battle Turns only.</remarks>
    BattleCompleted,

    /// <summary>
    /// The Player has been Eliminated from the game, and so will not be taking any further turns.
    /// </summary>
    Eliminated,

    /// <summary>
    /// The Player did not complete their turn in the allotted time, and so now has missed the opportunity to do so.
    /// </summary>
    TimedOut,

    /// <summary>
    /// The Player has abandoned the game, and so will not be taking any further turns.
    /// </summary>
    GameAbandoned
}
