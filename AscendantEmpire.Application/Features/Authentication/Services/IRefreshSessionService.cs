using AscendantEmpire.Application.Features.Authentication.Classes;

namespace AscendantEmpire.Application.Features.Authentication.Services;

/// <summary>
/// Manages the lifecycle of server-side login sessions (the revocable state behind refresh tokens): 
/// issuing them at login, rotating them on refresh, updating their game context on enter-game, and revoking them (logout, or kicking players / deleting games).
/// </summary>
public interface IRefreshSessionService
{
    /// <summary>
    /// Creates a new session for the user and returns the raw refresh token to hand back to the client.
    /// </summary>
    /// <param name="expiresAt">sets the absolute lifetime; game context is null until the
    /// user enters a game.</param>
    Task<IssuedSession> IssueAsync(int userId, int? gameId, int? playerId, DateTimeOffset expiresAt, CancellationToken cancelToken);

    /// <summary>
    /// Validates a raw refresh token against an active session and, on success, rotates the token
    /// (issues a new one and invalidates the old) and returns the context needed to mint a new access token. 
    /// </summary>
    /// <returns>Null when the token is unknown, revoked or expired.</returns>
    Task<RefreshedSession?> RotateAsync(string rawRefreshToken, CancellationToken cancelToken);

    /// <summary>
    /// Records the game and player a session has entered, so a later refresh re-issues a game-scoped access token and so the session can be revoked by game.
    /// </summary>
    Task UpdateGameContextAsync(int sessionId, int gameId, int playerId, CancellationToken cancelToken);

    /// <summary>
    /// Revokes a single session (logout). No-op if it is already revoked or missing.
    /// </summary>
    Task RevokeAsync(int sessionId, CancellationToken cancelToken);

    /// <summary>
    /// Revokes every active session currently scoped to the given game (e.g. when the game is deleted, kicking all its players). 
    /// </summary>
    /// <returns>the number of sessions revoked.</returns>
    Task<int> RevokeByGameAsync(int gameId, CancellationToken cancelToken);

    /// <summary>
    /// Revokes every active session currently scoped to the given player (kicking one player). 
    /// </summary>
    /// <returns>the number of sessions revoked.</returns>
    Task<int> RevokeByPlayerAsync(int playerId, CancellationToken cancelToken);

    /// <summary>
    /// Revokes every active session for a user (account ban / forced re-login).
    /// </summary>
    /// <returns>the number of sessions revoked.</returns>
    Task<int> RevokeByUserAsync(int userId, CancellationToken cancelToken);
}
