namespace AscendantEmpire.Domain.Enums.Turns;

public enum TurnQueueParticipantStatus
{
    /// <summary>
    /// Player is waiting for their turn. 
    /// </summary>
    /// <remarks>
    /// This is the default status when a new round starts, and is set for all active participants in the lane.
    /// Once the turn starts for a player, their status will be updated to TakingTurn.
    /// </remarks>
    Pending,

    /// <summary>
    /// The Player is current taking their turn.
    /// </summary>
    TakingTurn,

    /// <summary>
    /// The Player has had their turn for this Round, and completed their turn manually.
    /// </summary>
    TurnCompleted_Manual,

    /// <summary>
    /// The Player's turn was automatically completed, due to a timeout.
    /// </summary>
    TurnCompleted_Timeout,

    /// <summary>
    /// The Player has been eliminated from the Campaign or Battle, and is no longer an active participant in the Queue.
    /// </summary>
    Eliminated,

    /// <summary>
    /// The Player has withdrawn from the Battle and no longer has Troops active in it.
    /// </summary>
    /// <remarks>Battle Queue only.</remarks>
    Retreat,

    /// <summary>
    /// The Player left the Battle because their Troops routed.
    /// </summary>
    /// <remarks>Battle Queue only.</remarks>
    Routed,
}
