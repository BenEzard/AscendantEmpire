using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Domain.Entities.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;

namespace AscendantEmpire.Tests.Handlers.Authentication;

/// <summary>
/// Covers the refresh-session lifecycle (issue / rotate / revoke / update context) using the
/// in-memory provider. The service only loads-and-saves, so no SQL-Server-only features are used.
/// </summary>
internal class RefreshSessionServiceTests
{
    private static readonly ServiceProvider EfServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(RefreshSessionServiceTests), DbRoot)
        .UseInternalServiceProvider(EfServiceProvider)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private RefreshSessionService _service = null!;

    [SetUp]
    public async Task Setup()
    {
        await using (var setupContext = new AscendantEmpireContext(DbOptions))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        _dbFactory = new TestDbContextFactory<AscendantEmpireContext>(DbOptions);
        _service = new RefreshSessionService(NullLogger<RefreshSessionService>.Instance, _dbFactory);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => EfServiceProvider.Dispose();

    // --- Issue -------------------------------------------------------------------------------

    [Test, Description("Issuing a session persists it and stores only the hash of the raw token, never the token itself.")]
    public async Task Issue_ShouldStoreHashNotRawTokenAsync()
    {
        User user = await SeedUserAsync("alice");

        IssuedSession issued = await _service.IssueAsync(user.Id, gameId: null, playerId: null, FutureExpiry, CancellationToken.None);

        await using var context = new AscendantEmpireContext(DbOptions);
        RefreshSession stored = await context.RefreshSessions.SingleAsync(s => s.Id == issued.SessionId);

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

    // --- Rotate ------------------------------------------------------------------------------

    [Test, Description("Rotating a valid token returns the identity and a new token, and invalidates the old token.")]
    public async Task Rotate_ShouldReturnNewTokenAndInvalidateOldAsync()
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

        // The old token can no longer be used (it was rotated out).
        RefreshedSession? reusedOld = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        Assert.That(reusedOld, Is.Null);

        // The new token works.
        RefreshedSession? reusedNew = await _service.RotateAsync(refreshed!.RefreshToken, CancellationToken.None);
        Assert.That(reusedNew, Is.Not.Null);
    }

    [Test, Description("Rotating an unknown token returns null.")]
    public async Task Rotate_ShouldReturnNullForUnknownTokenAsync()
    {
        RefreshedSession? refreshed = await _service.RotateAsync("not-a-real-token", CancellationToken.None);
        Assert.That(refreshed, Is.Null);
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

    // --- Revoke ------------------------------------------------------------------------------

    [Test, Description("Revoking a session (logout) stops it from being refreshed.")]
    public async Task Revoke_ShouldStopRefreshAsync()
    {
        User user = await SeedUserAsync("erin");
        IssuedSession issued = await _service.IssueAsync(user.Id, null, null, FutureExpiry, CancellationToken.None);

        await _service.RevokeAsync(issued.SessionId, CancellationToken.None);

        RefreshedSession? refreshed = await _service.RotateAsync(issued.RefreshToken, CancellationToken.None);
        Assert.That(refreshed, Is.Null);
    }

    [Test, Description("Revoking by game revokes only sessions in that game and returns the count; other games are untouched.")]
    public async Task RevokeByGame_ShouldRevokeOnlyThatGameAsync()
    {
        User user = await SeedUserAsync("frank");

        IssuedSession inGame5A = await IssueInGameAsync(user.Id, gameId: 5, playerId: 1);
        IssuedSession inGame5B = await IssueInGameAsync(user.Id, gameId: 5, playerId: 2);
        IssuedSession inGame9 = await IssueInGameAsync(user.Id, gameId: 9, playerId: 3);

        int revoked = await _service.RevokeByGameAsync(5, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(revoked, Is.EqualTo(2));
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

    // --- Helpers -----------------------------------------------------------------------------

    private static DateTimeOffset FutureExpiry => DateTimeOffset.UtcNow.AddDays(14);

    private async Task<IssuedSession> IssueInGameAsync(int userId, int gameId, int playerId)
    {
        return await _service.IssueAsync(userId, gameId, playerId, FutureExpiry, CancellationToken.None);
    }

    private async Task<User> SeedUserAsync(string username)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

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
