namespace AscendantEmpire.Domain.Enums.Authentication;

/// <summary>
/// Why a <c>RefreshSession</c> was revoked.
/// </summary>
/// <remarks>
/// <para>
/// Recorded alongside the revocation timestamp so that a revoked session says why it was revoked rather than only that it was. The distinction that
/// matters operationally is <see cref="TokenReuseDetected"/>: every other member is routine housekeeping, while that one marks a session the server
/// believes was compromised.
/// </para>
/// <para>
/// Persisted as its underlying <see cref="int"/>, so members may be added but existing values must never be renumbered.
/// </para>
/// </remarks>
public enum RefreshRevocationReason
{
    /// <summary>The user logged out, revoking their own session.</summary>
    Logout = 0,

    /// <summary>
    /// An already-rotated refresh token was presented again, outside the benign double-refresh grace window.
    /// </summary>
    /// <remarks>
    /// Treated as a probable token theft: the whole token family is revoked, because the server cannot tell the thief's copy of the token from the
    /// legitimate client's.
    /// </remarks>
    TokenReuseDetected = 1,

    /// <summary>The owning account was banned, deleted or otherwise deactivated.</summary>
    UserDeactivated = 2,

    /// <summary>The game the session was scoped to was deleted, kicking all of its players.</summary>
    GameClosed = 3,

    /// <summary>The player the session was scoped to was removed from their game.</summary>
    PlayerRemoved = 4,

    /// <summary>Revoked by an administrator, with no more specific reason.</summary>
    Administrative = 5,
}
