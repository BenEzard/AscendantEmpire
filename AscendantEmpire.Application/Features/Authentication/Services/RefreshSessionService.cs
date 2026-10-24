using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Security.Cryptography;
using System.Text;

namespace AscendantEmpire.Application.Features.Authentication.Services;

/// <summary>
/// Database-backed implementation of <see cref="IRefreshSessionService"/>. Stores only the SHA-256
/// hash of each refresh token, so a leaked database row cannot be replayed as a token.
/// </summary>
internal class RefreshSessionService(
    ILogger<RefreshSessionService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IOptions<RefreshSessionSettings> settings
) : IRefreshSessionService
{
    // 32 random bytes give 256 bits of entropy — far beyond guessable.
    private const int _REFRESH_TOKEN_BYTES = 32;

    /// <summary>
    /// How many times a rotation will read-and-write before giving up and failing the refresh.
    /// </summary>
    /// <remarks>
    /// Two, i.e. one retry. A conflict means a concurrent refresh rotated the row between this one's read and its write; re-reading picks up that
    /// rotation and — inside the grace window — rotates again from it, so a single retry turns the race into a successful refresh. A second conflict is
    /// not worth chasing: the client can re-authenticate.
    /// </remarks>
    private const int _MAX_ROTATION_ATTEMPTS = 2;

    public async Task<IssuedSession> IssueAsync(int userId, int? gameId, int? playerId, DateTimeOffset expiresAt, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        string rawToken = GenerateRawToken();

        RefreshSession session = new()
        {
            UserId = userId,
            // A fresh family per login: every token this session later rotates to descends from this one, and revoking the family revokes the login.
            FamilyId = Guid.NewGuid(),
            TokenHash = HashToken(rawToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            GameId = gameId,
            PlayerId = playerId,
        };

        context.RefreshSessions.Add(session);
        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Issued refresh session {SessionId} for user {UserId}.", session.Id, userId);

        // Return the raw token (not the hash) — this is the only time it exists in plaintext.
        return new IssuedSession(session.Id, rawToken);
    }

    public async Task<RefreshedSession?> RotateAsync(string rawRefreshToken, CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return null;
        }

        string hash = HashToken(rawRefreshToken);

        for (int attempt = 1; attempt <= _MAX_ROTATION_ATTEMPTS; attempt++)
        {
            try
            {
                return await AttemptRotationAsync(hash, cancelToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A concurrent refresh rotated the row between this attempt's read and its write. Without the row version that write would have been
                // silently overwritten, leaving one of the two clients holding a token that never works again.
                if (attempt == _MAX_ROTATION_ATTEMPTS)
                {
                    logger.LogWarning("Refresh abandoned after {Attempts} concurrency conflicts; the client must re-authenticate.", _MAX_ROTATION_ATTEMPTS);
                    return null;
                }

                logger.LogInformation("Refresh lost a concurrency race on attempt {Attempt}; re-reading the session and retrying.", attempt);
            }
        }

        // Unreachable: the loop either returns a result or returns null on its final conflict.
        return null;
    }

    /// <summary>
    /// One read-validate-rotate pass over the session a token hash identifies.
    /// </summary>
    /// <remarks>
    /// Separated from <see cref="RotateAsync"/> so that a concurrency conflict can be answered by running the whole pass again against a fresh context,
    /// rather than retrying a write over a stale entity.
    /// </remarks>
    /// <returns>Null when the token is unknown, revoked, expired, or has been detected as reuse.</returns>
    private async Task<RefreshedSession?> AttemptRotationAsync(string hash, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Match either hash column. A hit on the live hash is an ordinary refresh; a hit on the retired one is either a replayed token or the benign
        // double-refresh below, and telling those two apart is what this method exists for.
        RefreshSession? session = await context.RefreshSessions
            .FirstOrDefaultAsync(s => s.TokenHash == hash || s.PreviousTokenHash == hash, cancelToken);

        if (session is null)
        {
            logger.LogInformation("Refresh rejected: token is unknown.");
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // The token presented is the one the most recent rotation retired, not the one it issued.
        bool presentedRetiredToken = session.TokenHash != hash;

        if (session.IsActive(now) == false)
        {
            // Nothing left to protect on a revoked or expired session, so a retired token here is not worth raising as a compromise. This is also what
            // keeps a detected reuse from logging a fresh warning on every further replay: the first one revoked the session, so the rest land here.
            logger.LogInformation(
                "Refresh rejected: session {SessionId} is revoked or expired (retired token presented: {PresentedRetiredToken}).",
                session.Id,
                presentedRetiredToken);
            return null;
        }

        if (presentedRetiredToken && IsWithinReuseGrace(session, now) == false)
        {
            // The signal this whole feature exists for. Before it, a replayed token was indistinguishable from a typo: a bare 401, and whoever
            // refreshed first simply kept the session. Warning rather than Information because this is the one revocation reason that means something
            // went wrong rather than something ended.
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId} on session {SessionId} (family {FamilyId}); revoking the whole token family as a probable compromise.",
                session.UserId,
                session.Id,
                session.FamilyId);

            await RevokeFamilyOnContextAsync(context, session.FamilyId, RefreshRevocationReason.TokenReuseDetected, now, cancelToken);
            return null;
        }

        if (presentedRetiredToken)
        {
            // Two clients racing the same refresh — two browser tabs are two independent single-flight guards. Rotating again hands this one a working
            // token instead of logging the user out over a race they cannot see.
            logger.LogInformation(
                "Refresh presented the token retired by session {SessionId}'s last rotation, inside the {GraceSeconds}s grace window; treating it as a concurrent refresh rather than reuse.",
                session.Id,
                settings.Value.ReuseGraceSeconds);
        }

        // The username is needed to mint the new access token; read it from the proven user record.
        string username = await context.Users
            .Where(u => u.Id == session.UserId)
            .Select(u => u.Username)
            .FirstAsync(cancelToken);

        // Re-read the grants on every rotation rather than carrying them over from the expiring token.
        // This is what bounds how long a revoked permission keeps working: one access-token lifetime.
        IReadOnlyCollection<PermissionType> permissions = await context.GetUserPermissionsAsync(session.UserId, cancelToken);

        // Rotate: a new secret replaces the old, keeping the same absolute expiry so refreshing can't extend a session indefinitely. The outgoing hash
        // is retired rather than discarded, which is what makes its reappearance detectable. CreatedAt is deliberately left alone — it records when
        // the session began, not when it was last refreshed.
        string newRawToken = GenerateRawToken();
        session.PreviousTokenHash = session.TokenHash;
        session.TokenHash = HashToken(newRawToken);
        session.LastRotatedAt = now;

        await context.SaveChangesAsync(cancelToken);

        return new RefreshedSession(session.Id, session.UserId, username, session.GameId, session.PlayerId, newRawToken, permissions);
    }

    /// <summary>
    /// Whether a retired token was presented soon enough after its rotation to be a concurrent refresh rather than a replay.
    /// </summary>
    private bool IsWithinReuseGrace(RefreshSession session, DateTimeOffset now)
    {
        int graceSeconds = settings.Value.ReuseGraceSeconds;

        // Zero (or a nonsensical negative) switches the grace off: every replay is then reuse.
        if (graceSeconds <= 0)
        {
            return false;
        }

        // No rotation timestamp means nothing was retired by a rotation this code performed, so there is no window to be inside. Only rows predating
        // the reuse-detection migration can be in that state.
        if (session.LastRotatedAt is null)
        {
            return false;
        }

        return now < session.LastRotatedAt.Value.AddSeconds(graceSeconds);
    }

    /// <summary>
    /// Revokes every still-active session in a token family on the caller's context, without saving.
    /// </summary>
    /// <remarks>
    /// Shares the caller's context so that the revocation and whatever prompted it commit together — a reuse detection must not be able to half-apply.
    /// </remarks>
    private async Task<int> RevokeFamilyOnContextAsync(
        AscendantEmpireContext context,
        Guid familyId,
        RefreshRevocationReason reason,
        DateTimeOffset now,
        CancellationToken cancelToken)
    {
        List<RefreshSession> familySessions = await context.RefreshSessions
            .Where(s => s.FamilyId == familyId)
            .Where(s => s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(cancelToken);

        foreach (RefreshSession familySession in familySessions)
        {
            familySession.RevokedAt = now;
            familySession.RevokedReason = reason;
        }

        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Revoked {Count} session(s) in family {FamilyId} ({Reason}).", familySessions.Count, familyId, reason);
        return familySessions.Count;
    }

    public async Task UpdateGameContextAsync(int sessionId, int gameId, int playerId, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        RefreshSession? session = await context.RefreshSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancelToken);

        if (session is null)
        {
            // The session backing the caller's token has gone (e.g. revoked and pruned). Nothing
            // to update; the next refresh will fail and log the user out.
            logger.LogWarning("Could not update game context: session {SessionId} not found.", sessionId);
            return;
        }

        session.GameId = gameId;
        session.PlayerId = playerId;

        await context.SaveChangesAsync(cancelToken);
    }

    public async Task RevokeAsync(int sessionId, RefreshRevocationReason reason, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        RefreshSession? session = await context.RefreshSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.RevokedAt == null, cancelToken);

        if (session is null)
        {
            // Already revoked or never existed — logout is idempotent.
            return;
        }

        session.RevokedAt = DateTimeOffset.UtcNow;
        session.RevokedReason = reason;
        await context.SaveChangesAsync(cancelToken);
    }

    public Task<int> RevokeByFamilyAsync(Guid familyId, RefreshRevocationReason reason, CancellationToken cancelToken)
    {
        return RevokeWhereAsync(s => s.FamilyId == familyId, reason, cancelToken);
    }

    public Task<int> RevokeByGameAsync(int gameId, CancellationToken cancelToken)
    {
        return RevokeWhereAsync(s => s.GameId == gameId, RefreshRevocationReason.GameClosed, cancelToken);
    }

    public Task<int> RevokeByPlayerAsync(int playerId, CancellationToken cancelToken)
    {
        return RevokeWhereAsync(s => s.PlayerId == playerId, RefreshRevocationReason.PlayerRemoved, cancelToken);
    }

    public Task<int> RevokeByUserAsync(int userId, CancellationToken cancelToken)
    {
        return RevokeWhereAsync(s => s.UserId == userId, RefreshRevocationReason.UserDeactivated, cancelToken);
    }

    public async Task<int> PruneExpiredSessionsAsync(CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Prune by expiry alone, not by revocation. A revoked session's row is the only record that it was revoked and why, and a reuse detection is
        // worth keeping around to look at; its expiry arrives soon enough.
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-settings.Value.RetentionDays);

        int pruned = await context.RefreshSessions
            .Where(s => s.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancelToken);

        if (pruned > 0)
        {
            logger.LogInformation("Pruned {Count} refresh session(s) that expired before {Cutoff:O}.", pruned, cutoff);
        }

        return pruned;
    }

    /// <summary>
    /// Revokes all currently-active sessions matching the predicate, stamping them revoked as of
    /// now with the given reason. Revocation is infrequent (game delete / kick / ban) and touches
    /// only a handful of rows, so loading and saving them keeps the code provider-agnostic and easy
    /// to test. Returns how many sessions were revoked.
    /// </summary>
    private async Task<int> RevokeWhereAsync(
        System.Linq.Expressions.Expression<Func<RefreshSession, bool>> match,
        RefreshRevocationReason reason,
        CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Only touch sessions that are still active (not already revoked and not past expiry).
        List<RefreshSession> sessions = await context.RefreshSessions
            .Where(match)
            .Where(s => s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(cancelToken);

        foreach (RefreshSession session in sessions)
        {
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }

        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Revoked {Count} refresh session(s) ({Reason}).", sessions.Count, reason);
        return sessions.Count;
    }

    /// <summary>A URL-safe, cryptographically-random opaque token (hex of 32 random bytes).</summary>
    private static string GenerateRawToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(_REFRESH_TOKEN_BYTES);
        return Convert.ToHexString(bytes);
    }

    /// <summary>SHA-256 hash (hex) of a raw token, as stored and looked up in the database.</summary>
    private static string HashToken(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }
}
