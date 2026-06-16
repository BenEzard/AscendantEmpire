namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Names of the claims we embed in our JWTs. 
/// </summary>
/// <remarks>
/// Shared between the code that writes tokens (<see cref="JwtTokenService"/>) and the code that reads them (<see cref="AscendantEmpire.Api.Utilities.ClaimsPrincipalExtensions"/>).
/// </remarks>
public static class AuthClaimTypes
{
    /// <summary>
    /// The user's account id. Present in every token.
    /// </summary>
    public const string UserId = "userId";

    /// <summary>The user's login name. Present in every token.</summary>
    public const string Username = "username";

    /// <summary>
    /// The id of the server-side <see cref="AscendantEmpire.Domain.Entities.Identity.RefreshSession"/> this token was issued from. 
    /// </summary>
    /// <remarks>
    /// Present in every token; lets enter-game/logout act on the session behind the bearer token. Revoking that session stops further refreshes.
    /// </remarks>
    public const string SessionId = "sessionId";

    /// <summary>
    /// The entered game's id. Present only in game-scoped tokens.
    /// </summary>
    public const string GameId = "gameId";

    /// <summary>
    /// The player the user controls in the entered game. Present only in game-scoped tokens.
    /// </summary>
    public const string PlayerId = "playerId";
}
