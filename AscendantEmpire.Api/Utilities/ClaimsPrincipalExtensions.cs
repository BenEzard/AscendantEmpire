using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Domain.Enums.Authentication;

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

    /// <summary>
    /// Every permission the token grants. Empty when the token carries none.
    /// </summary>
    /// <remarks>
    /// Uses <c>FindAll</c> because the permission claim is repeated once per grant, unlike the single-valued identity claims above. Values that don't parse
    /// as a known <see cref="PermissionType"/> are skipped rather than throwing — a token signed before an enum member was renamed should degrade to
    /// "doesn't hold that permission", not to a 500.
    /// </remarks>
    public static IReadOnlyCollection<PermissionType> GetPermissions(this ClaimsPrincipal user)
    {
        List<PermissionType> permissions = [];

        foreach (Claim claim in user.FindAll(AuthClaimTypes.Permission))
        {
            if (Enum.TryParse(claim.Value, out PermissionType permission))
            {
                permissions.Add(permission);
            }
        }

        return permissions;
    }

    /// <summary>
    /// True when the token grants the given permission.
    /// </summary>
    /// <remarks>
    /// The authorization policies are the enforcement point; this is for the cases that need to vary behaviour rather than reject the request outright.
    /// </remarks>
    public static bool HasPermission(this ClaimsPrincipal user, PermissionType permission)
    {
        return user.HasClaim(AuthClaimTypes.Permission, permission.ToString());
    }

    private static int? ReadInt(ClaimsPrincipal user, string claimType)
    {
        string? raw = user.FindFirst(claimType)?.Value;
        return int.TryParse(raw, out int value) ? value : null;
    }
}
