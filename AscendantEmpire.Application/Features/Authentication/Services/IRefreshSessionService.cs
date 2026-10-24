using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Domain.Enums.Authentication;

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
    /// (issues a new one and retires the old) and returns the context needed to mint a new access token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rotation retires the outgoing hash rather than discarding it, so presenting an already-exchanged token is detectable. Outside a short grace
    /// window that is read as a stolen token: the whole token family is revoked with
    /// <see cref="RefreshRevocationReason.TokenReuseDetected"/> and a warning is logged naming the user. Inside the window it is read as two clients
    /// racing one refresh, and the session simply rotates again.
    /// </para>
    /// <para>
    /// Both outcomes are a null return, because the caller's answer to the client is a 401 either way. The difference is the audit trail, which is the
    /// point: a replayed token used to be indistinguishable from an unknown one.
    /// </para>
    /// </remarks>
    /// <returns>Null when the token is unknown, revoked, expired, or was detected as reuse.</returns>
    Task<RefreshedSession?> RotateAsync(string rawRefreshToken, CancellationToken cancelToken);

    /// <summary>
    /// Records the game and player a session has entered, so a later refresh re-issues a game-scoped access token and so the session can be revoked by game.
    /// </summary>
    Task UpdateGameContextAsync(int sessionId, int gameId, int playerId, CancellationToken cancelToken);

    /// <summary>
    /// Revokes a single session (logout). No-op if it is already revoked or missing.
    /// </summary>
    /// <param name="reason">recorded on the session, so a revoked row says why it was revoked and not merely that it was.</param>
    Task RevokeAsync(int sessionId, RefreshRevocationReason reason, CancellationToken cancelToken);

    /// <summary>
    /// Revokes every active session in one token family — every rotation descended from a single login.
    /// </summary>
    /// <remarks>
    /// This is what a detected token reuse triggers: the server cannot tell the thief's copy of the token from the legitimate client's, so neither is
    /// allowed to continue.
    /// </remarks>
    /// <returns>the number of sessions revoked.</returns>
    Task<int> RevokeByFamilyAsync(Guid familyId, RefreshRevocationReason reason, CancellationToken cancelToken);

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

    /// <summary>
    /// Deletes session rows whose absolute expiry passed longer ago than the configured retention, and returns how many were deleted.
    /// </summary>
    /// <remarks>
    /// Every login adds a row and nothing ever removed one, so the table and its unique indexes grew without bound. Expiry is the only condition:
    /// revoked-but-unexpired rows are kept, because a session revoked for token reuse is the record that it happened.
    /// </remarks>
    Task<int> PruneExpiredSessionsAsync(CancellationToken cancelToken);
}
