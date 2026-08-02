using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Security.Cryptography;
using System.Text;

namespace AscendantEmpire.Application.Features.Authentication.Services;

/// <summary>
/// Database-backed implementation of <see cref="IRefreshSessionService"/>. Stores only the SHA-256
/// hash of each refresh token, so a leaked database row cannot be replayed as a token.
/// </summary>
internal class RefreshSessionService(
    ILogger<RefreshSessionService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRefreshSessionService
{
    // 32 random bytes give 256 bits of entropy — far beyond guessable.
    private const int RefreshTokenBytes = 32;

    public async Task<IssuedSession> IssueAsync(int userId, int? gameId, int? playerId, DateTimeOffset expiresAt, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        string rawToken = GenerateRawToken();

        RefreshSession session = new()
        {
            UserId = userId,
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

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        string hash = HashToken(rawRefreshToken);

        RefreshSession? session = await context.RefreshSessions
            .FirstOrDefaultAsync(s => s.TokenHash == hash, cancelToken);

        // Unknown, revoked or expired tokens all fail the same way — the caller returns 401.
        if (session is null || session.IsActive(DateTimeOffset.UtcNow) == false)
        {
            logger.LogInformation("Refresh rejected: token is unknown, revoked or expired.");
            return null;
        }

        // The username is needed to mint the new access token; read it from the proven user record.
        string username = await context.Users
            .Where(u => u.Id == session.UserId)
            .Select(u => u.Username)
            .FirstAsync(cancelToken);

        // Re-read the grants on every rotation rather than carrying them over from the expiring token.
        // This is what bounds how long a revoked permission keeps working: one access-token lifetime.
        IReadOnlyCollection<PermissionType> permissions = await context.GetUserPermissionsAsync(session.UserId, cancelToken);

        // Rotate the token in place: a new secret replaces the old, keeping the same absolute
        // expiry so refreshing can't extend a session indefinitely.
        string newRawToken = GenerateRawToken();
        session.TokenHash = HashToken(newRawToken);
        session.CreatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancelToken);

        return new RefreshedSession(session.Id, session.UserId, username, session.GameId, session.PlayerId, newRawToken, permissions);
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

    public async Task RevokeAsync(int sessionId, CancellationToken cancelToken)
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
        await context.SaveChangesAsync(cancelToken);
    }

    public Task<int> RevokeByGameAsync(int gameId, CancellationToken cancelToken)
    {
        return RevokeWhereAsync(s => s.GameId == gameId, cancelToken);
    }

    public Task<int> RevokeByPlayerAsync(int playerId, CancellationToken cancelToken)
    {
        return RevokeWhereAsync(s => s.PlayerId == playerId, cancelToken);
    }

    public Task<int> RevokeByUserAsync(int userId, CancellationToken cancelToken)
    {
        return RevokeWhereAsync(s => s.UserId == userId, cancelToken);
    }

    /// <summary>
    /// Revokes all currently-active sessions matching the predicate, stamping them revoked as of
    /// now. Revocation is infrequent (game delete / kick / ban) and touches only a handful of
    /// rows, so loading and saving them keeps the code provider-agnostic and easy to test.
    /// Returns how many sessions were revoked.
    /// </summary>
    private async Task<int> RevokeWhereAsync(
        System.Linq.Expressions.Expression<Func<RefreshSession, bool>> match,
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
        }

        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Revoked {Count} refresh session(s).", sessions.Count);
        return sessions.Count;
    }

    /// <summary>A URL-safe, cryptographically-random opaque token (hex of 32 random bytes).</summary>
    private static string GenerateRawToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(RefreshTokenBytes);
        return Convert.ToHexString(bytes);
    }

    /// <summary>SHA-256 hash (hex) of a raw token, as stored and looked up in the database.</summary>
    private static string HashToken(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }
}
