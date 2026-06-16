using AscendantEmpire.Api.Authentication;

using System.Security.Claims;

namespace AscendantEmpire.Api.Utilities;

/// <summary>
/// Helpers for reading our custom claims off the authenticated user.
/// </summary>
/// <remarks>
/// Controllers use these to pull the user / game / player out of the bearer token instead of taking them from the request body or query string.
/// </remarks>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The user's account id, or null if the claim is missing or unparseable.
    /// </summary>
    public static int? GetUserId(this ClaimsPrincipal user) => ReadInt(user, AuthClaimTypes.UserId);

    /// <summary>
    /// The id of the session the token was issued from, or null if absent/unparseable.
    /// </summary>
    public static int? GetSessionId(this ClaimsPrincipal user) => ReadInt(user, AuthClaimTypes.SessionId);

    /// <summary>
    /// The entered game's id. Null on a user token that hasn't entered a game yet.
    /// </summary>
    public static int? GetGameId(this ClaimsPrincipal user) => ReadInt(user, AuthClaimTypes.GameId);

    /// <summary>
    /// The controlled player's id. Null on a user token that hasn't entered a game yet.
    /// </summary>
    public static int? GetPlayerId(this ClaimsPrincipal user) => ReadInt(user, AuthClaimTypes.PlayerId);

    private static int? ReadInt(ClaimsPrincipal user, string claimType)
    {
        string? raw = user.FindFirst(claimType)?.Value;
        return int.TryParse(raw, out int value) ? value : null;
    }
}
