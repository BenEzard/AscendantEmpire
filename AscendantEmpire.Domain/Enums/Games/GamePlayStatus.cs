namespace AscendantEmpire.Domain.Enums.Games;

/// <summary>
/// Where a Game has got to in its lifecycle, from creation through to the end of play.
/// </summary>
/// <remarks>
/// This is separate from <see cref="GameCreationStatus"/>, which tracks only whether the Game's data has finished being built. 
/// </remarks>
public enum GamePlayStatus
{
    /// <summary>The Game's data is still being built and it is not yet visible to Players.</summary>
    SetupInProgress,

    /// <summary>Setup has finished and the Game is listed in the Match Maker, but Players are still claiming slots.</summary>
    AwaitingPlayers,

    /// <summary>Every slot has been claimed and the Game is being played.</summary>
    InProgress,

    /// <summary>The Game has reached a result and is over.</summary>
    Completed,

    /// <summary>The Game was given up on before reaching a result.</summary>
    Abandoned
}
