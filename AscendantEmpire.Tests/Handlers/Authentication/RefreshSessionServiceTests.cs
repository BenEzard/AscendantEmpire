using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AscendantEmpire.Tests.Handlers.Authentication;

/// <summary>
/// Covers the refresh-session lifecycle — issue, rotate, reuse detection and revoke — on the in-memory provider.
/// </summary>
/// <remarks>
/// <para>
/// Tier 2 per <c>testing.md</c>: the service only loads and saves, and reuse detection is decided in code from the two hash columns rather than by a
/// database constraint, so nothing here needs a real engine. The filtered unique index on <c>PreviousTokenHash</c> and the <c>rowversion</c> behind the
/// concurrency retry are database-enforced and therefore out of this fixture's reach.
/// </para>
/// <para>
/// Most tests run against a service with the reuse grace window switched off, so that replaying a retired token is unambiguously reuse. The grace window
/// has tests of its own, which build a service with a window long enough that a replay lands inside it.
/// </para>
/// </remarks>
internal class RefreshSessionServiceTests : InMemoryTestBase
{
    private RefreshSessionService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = CreateService(reuseGraceSeconds: 0);
    }

    // --- Issue -------------------------------------------------------------------------------

    [Test, Description("Issuing a session persists it and stores only the hash of the raw token, never the token itself.")]
    public async Task Issue_ShouldStoreHashNotRawTokenAsync()
    {
        User user = await SeedUserAsync("alice");

        IssuedSession issued = await _service.IssueAsync(user.Id, gameId: null, playerId: null, FutureExpiry, CancellationToken.None);

        RefreshSession stored = await LoadSessionAsync(issued.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(issued.RefreshToken, Is.Not.Empty);
            // The raw token must never be stored verbatim.
            Assert.That(stored.TokenHash, Is.Not.EqualTo(issued.RefreshToken));
            Assert.That(stored.UserId, Is.EqualTo(user.Id));
            Assert.That(stored.RevokedAt, Is.Null);
            Assert.That(stored.GameId, Is.Null);
        }
    }

    [Test, Description("A newly issued session starts a family of its own and has no retired hash to replay yet.")]
    public async Task Issue_ShouldStartANewTokenFamilyAsync()
    {
        User user = await SeedUserAsync("anna");

        IssuedSession first = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);
        IssuedSession second = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        RefreshSession storedFirst = await LoadSessionAsync(first.SessionId);
        RefreshSession storedSecond = await LoadSessionAsync(second.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(storedFirst.FamilyId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(storedFirst.PreviousTokenHash, Is.Null);
            Assert.That(storedFirst.LastRotatedAt, Is.Null);
            // Two logins by the same user are two families, so revoking one cannot touch the other.
            Assert.That(storedSecond.FamilyId, Is.Not.EqualTo(storedFirst.FamilyId));
        }
    }

    // --- Rotate ------------------------------------------------------------------------------

    [Test, Description("Rotating a valid token returns the identity and a new token, and the session can then be rotated again with it.")]
    public async Task Rotate_ShouldReturnNewTokenAndStayUsableAsync()
    {
        User user = await SeedUserAsync("bob");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        RefreshedSession? refreshed = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refreshed, Is.Not.Null);
            Assert.That(refreshed!.SessionId, Is.EqualTo(issued.SessionId));
            Assert.That(refreshed.UserId, Is.EqualTo(user.Id));
            Assert.That(refreshed.Username, Is.EqualTo("bob"));
            // A new token is issued, different from the one just consumed.
            Assert.That(refreshed.RefreshToken, Is.Not.EqualTo(issued.RefreshToken));
        }

        // The token just issued is the one that works from here on.
        RefreshedSession? refreshedAgain = await _service.RotateAsync(refreshed!.RefreshToken, CancellationToken.None);
        Assert.That(refreshedAgain, Is.Not.Null);
    }

    [Test, Description("Rotating retires the outgoing hash to PreviousTokenHash rather than overwriting it, which is what makes a replay detectable.")]
    public async Task Rotate_ShouldRetireTheOutgoingHashAsync()
    {
        User user = await SeedUserAsync("bella");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        string hashAtIssue = (await LoadSessionAsync(issued.SessionId)).TokenHash;

        await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        RefreshSession rotated = await LoadSessionAsync(issued.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rotated.PreviousTokenHash, Is.EqualTo(hashAtIssue));
            Assert.That(rotated.TokenHash, Is.Not.EqualTo(hashAtIssue));
        }
    }

    [Test, Description("Rotating leaves CreatedAt alone and stamps LastRotatedAt, so CreatedAt records when the session began rather than its last refresh.")]
    public async Task Rotate_ShouldPreserveCreatedAtAndStampLastRotatedAtAsync()
    {
        User user = await SeedUserAsync("brian");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        DateTimeOffset createdAtIssue = (await LoadSessionAsync(issued.SessionId)).CreatedAt;

        await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        RefreshSession rotated = await LoadSessionAsync(issued.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rotated.CreatedAt, Is.EqualTo(createdAtIssue));
            Assert.That(rotated.LastRotatedAt, Is.Not.Null);
            Assert.That(rotated.LastRotatedAt, Is.GreaterThanOrEqualTo(createdAtIssue));
        }
    }

    [Test, Description("Rotating an unknown token returns null.")]
    public async Task Rotate_ShouldReturnNullForUnknownTokenAsync()
    {
        RefreshedSession? refreshed = await _service.RotateAsync("not-a-real-token", CancellationToken.None);
        Assert.That(refreshed, Is.Null);
    }

    [Test, Description("An unknown token revokes nothing, so a stray or mistyped token cannot be used to log a legitimate session out.")]
    public async Task Rotate_ShouldNotRevokeAnythingForUnknownTokenAsync()
    {
        User user = await SeedUserAsync("casey");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await _service.RotateAsync("not-a-real-token", CancellationToken.None);

        RefreshSession stored = await LoadSessionAsync(issued.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.RevokedAt, Is.Null);
            Assert.That(stored.RevokedReason, Is.Null);
        }
    }

    [Test, Description("Rotating an expired token returns null.")]
    public async Task Rotate_ShouldReturnNullForExpiredTokenAsync()
    {
        User user = await SeedUserAsync("carol");
        // Issue already past its expiry.
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None);

        RefreshedSession? refreshed = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        Assert.That(refreshed, Is.Null);
    }

    [Test, Description("After entering a game, a rotate returns the stored game context so a game-scoped token can be minted.")]
    public async Task Rotate_ShouldCarryGameContextAfterUpdateAsync()
    {
        User user = await SeedUserAsync("dave");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await _service.UpdateGameContextAsync(issued.SessionId, gameId: 42, playerId: 7, CancellationToken.None);

        RefreshedSession? refreshed = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refreshed, Is.Not.Null);
            Assert.That(refreshed!.GameId, Is.EqualTo(42));
            Assert.That(refreshed.PlayerId, Is.EqualTo(7));
        }
    }

    // --- Reuse detection ---------------------------------------------------------------------

    [Test, Description("Replaying an already-rotated token outside the grace window refuses the refresh and revokes the session as a detected reuse.")]
    public async Task Rotate_ShouldRevokeFamilyWhenRetiredTokenIsReplayedAsync()
    {
        User user = await SeedUserAsync("mallory");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        // The legitimate client refreshes, retiring the token the attacker also holds.
        RefreshedSession? refreshed = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        Assert.That(refreshed, Is.Not.Null);

        RefreshedSession? replayed = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        RefreshSession stored = await LoadSessionAsync(issued.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Is.Null);
            Assert.That(stored.RevokedAt, Is.Not.Null);
            Assert.That(stored.RevokedReason, Is.EqualTo(RefreshRevocationReason.TokenReuseDetected));
        }

        // The legitimate client's token dies with the family: the server cannot tell which holder was the thief.
        RefreshedSession? afterDetection = await _service.RotateAsync(refreshed!.RefreshToken, CancellationToken.None);
        Assert.That(afterDetection, Is.Null);
    }

    [Test, Description("A detected reuse logs at Warning naming the user, which is the signal that was missing when a replay failed silently as an unknown token.")]
    public async Task Rotate_ShouldWarnWhenRetiredTokenIsReplayedAsync()
    {
        CapturingLogger<RefreshSessionService> logger = new();
        RefreshSessionService service = CreateService(reuseGraceSeconds: 0, logger: logger);

        User user = await SeedUserAsync("morgan");
        IssuedSession issued = await service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        await service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        List<CapturedLogEntry> warnings = logger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0].Message, Does.Contain("reuse detected"));
            Assert.That(warnings[0].Message, Does.Contain(user.Id.ToString()));
        }
    }

    [Test, Description("Further replays after a detection do not raise a second warning, so a repeating attacker cannot flood the log.")]
    public async Task Rotate_ShouldNotWarnAgainOnFurtherReplaysAsync()
    {
        CapturingLogger<RefreshSessionService> logger = new();
        RefreshSessionService service = CreateService(reuseGraceSeconds: 0, logger: logger);

        User user = await SeedUserAsync("nadia");
        IssuedSession issued = await service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        // First replay detects, the next two land on an already-revoked session.
        await service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        await service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        await service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        int warnings = logger.Entries.Count(entry => entry.Level == LogLevel.Warning);
        Assert.That(warnings, Is.EqualTo(1));
    }

    [Test, Description("Replaying a retired token inside the grace window rotates again instead of revoking, so two tabs racing one refresh are not logged out.")]
    public async Task Rotate_ShouldRotateAgainInsideTheGraceWindowAsync()
    {
        // Long enough that a replay in the same test is unambiguously inside the window.
        RefreshSessionService service = CreateService(reuseGraceSeconds: 600);

        User user = await SeedUserAsync("tabitha");
        IssuedSession issued = await service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        // The first tab wins the race.
        RefreshedSession? firstTab = await service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        // The second tab presents the same token a moment later.
        RefreshedSession? secondTab = await service.RotateAsync(issued.RefreshToken, CancellationToken.None);

        RefreshSession stored = await LoadSessionAsync(issued.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstTab, Is.Not.Null);
            Assert.That(secondTab, Is.Not.Null);
            Assert.That(secondTab!.RefreshToken, Is.Not.EqualTo(firstTab!.RefreshToken));
            // Nothing was treated as a compromise.
            Assert.That(stored.RevokedAt, Is.Null);
            Assert.That(stored.RevokedReason, Is.Null);
        }
    }

    [Test, Description("A detected reuse revokes only the offending family, so the same user's other logins keep working.")]
    public async Task Rotate_ShouldLeaveOtherFamiliesIntactOnReuseAsync()
    {
        User user = await SeedUserAsync("olivia");

        IssuedSession compromised = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);
        IssuedSession otherDevice = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await _service.RotateAsync(compromised.RefreshToken, CancellationToken.None);
        await _service.RotateAsync(compromised.RefreshToken, CancellationToken.None);

        RefreshSession storedOther = await LoadSessionAsync(otherDevice.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(storedOther.RevokedAt, Is.Null);
            Assert.That(_service.RotateAsync(otherDevice.RefreshToken, CancellationToken.None).Result, Is.Not.Null);
        }
    }

    // --- Revoke ------------------------------------------------------------------------------

    [Test, Description("Revoking a session (logout) stops it from being refreshed.")]
    public async Task Revoke_ShouldStopRefreshAsync()
    {
        User user = await SeedUserAsync("erin");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await _service.RevokeAsync(issued.SessionId, RefreshRevocationReason.Logout, CancellationToken.None);

        RefreshedSession? refreshed = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        Assert.That(refreshed, Is.Null);
    }

    [Test, Description("Revoking records the caller's reason, so a revoked row says why it was revoked and not merely that it was.")]
    public async Task Revoke_ShouldRecordTheReasonAsync()
    {
        User user = await SeedUserAsync("eve");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await _service.RevokeAsync(issued.SessionId, RefreshRevocationReason.Administrative, CancellationToken.None);

        RefreshSession stored = await LoadSessionAsync(issued.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.RevokedAt, Is.Not.Null);
            Assert.That(stored.RevokedReason, Is.EqualTo(RefreshRevocationReason.Administrative));
        }
    }

    [Test, Description("Revoking by game revokes only sessions in that game and returns the count; other games are untouched.")]
    public async Task RevokeByGame_ShouldRevokeOnlyThatGameAsync()
    {
        User user = await SeedUserAsync("frank");

        IssuedSession inGame5A = await IssueInGameAsync(user.Id, gameId: 5, playerId: 1);
        IssuedSession inGame5B = await IssueInGameAsync(user.Id, gameId: 5, playerId: 2);
        IssuedSession inGame9 = await IssueInGameAsync(user.Id, gameId: 9, playerId: 3);

        int revoked = await _service.RevokeByGameAsync(5, CancellationToken.None);

        RefreshSession storedGame5A = await LoadSessionAsync(inGame5A.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(revoked, Is.EqualTo(2));
            // The reason comes from the revocation axis, not the caller, so a game delete is always recorded as such.
            Assert.That(storedGame5A.RevokedReason, Is.EqualTo(RefreshRevocationReason.GameClosed));
            // Both game-5 sessions can no longer refresh.
            Assert.That(_service.RotateAsync(inGame5A.RefreshToken, CancellationToken.None).Result, Is.Null);
            Assert.That(_service.RotateAsync(inGame5B.RefreshToken, CancellationToken.None).Result, Is.Null);
            // The game-9 session is unaffected.
            Assert.That(_service.RotateAsync(inGame9.RefreshToken, CancellationToken.None).Result, Is.Not.Null);
        }
    }

    [Test, Description("Revoking by player revokes only that player's session.")]
    public async Task RevokeByPlayer_ShouldRevokeOnlyThatPlayerAsync()
    {
        User user = await SeedUserAsync("grace");

        IssuedSession player1 = await IssueInGameAsync(user.Id, gameId: 3, playerId: 11);
        IssuedSession player2 = await IssueInGameAsync(user.Id, gameId: 3, playerId: 12);

        int revoked = await _service.RevokeByPlayerAsync(11, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(revoked, Is.EqualTo(1));
            Assert.That(_service.RotateAsync(player1.RefreshToken, CancellationToken.None).Result, Is.Null);
            Assert.That(_service.RotateAsync(player2.RefreshToken, CancellationToken.None).Result, Is.Not.Null);
        }
    }

    [Test, Description("Revoking by family revokes that family alone, guarding against a revoke whose predicate forgets the family filter.")]
    public async Task RevokeByFamily_ShouldRevokeOnlyThatFamilyAsync()
    {
        User user = await SeedUserAsync("heidi");

        IssuedSession target = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);
        IssuedSession bystander = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        Guid targetFamily = (await LoadSessionAsync(target.SessionId)).FamilyId;

        int revoked = await _service.RevokeByFamilyAsync(targetFamily, RefreshRevocationReason.TokenReuseDetected, CancellationToken.None);

        RefreshSession storedBystander = await LoadSessionAsync(bystander.SessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(revoked, Is.EqualTo(1));
            Assert.That(storedBystander.RevokedAt, Is.Null);
        }
    }

    // --- Helpers -----------------------------------------------------------------------------

    private static DateTimeOffset FutureExpiry => DateTimeOffset.UtcNow.AddDays(14);

    /// <summary>
    /// A service over this test's store, with the reuse grace window set to the given length.
    /// </summary>
    /// <param name="reuseGraceSeconds">zero switches the grace off, so any replay of a retired token counts as reuse.</param>
    private RefreshSessionService CreateService(int reuseGraceSeconds, ILogger<RefreshSessionService>? logger = null)
    {
        RefreshSessionSettings settings = new() { ReuseGraceSeconds = reuseGraceSeconds };

        return new RefreshSessionService(
            logger ?? NullLogger<RefreshSessionService>.Instance,
            DbFactory,
            MsOptions.Create(settings));
    }

    private async Task<IssuedSession> IssueInGameAsync(int userId, int gameId, int playerId)
    {
        return await _service.IssueAsync(userId, gameId, playerId, FutureExpiry, CancellationToken.None);
    }

    /// <summary>Reads a session back through a fresh context, so assertions see what was persisted rather than a tracked entity.</summary>
    private async Task<RefreshSession> LoadSessionAsync(int sessionId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        return await context.RefreshSessions.SingleAsync(s => s.Id == sessionId);
    }

    private async Task<User> SeedUserAsync(string username)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        User user = new()
        {
            Username = username,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
