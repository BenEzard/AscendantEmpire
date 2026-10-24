using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AscendantEmpire.Tests.Handlers.Authentication;

/// <summary>
/// Covers the pruning sweep that keeps the refresh-session table from growing with every login.
/// </summary>
/// <remarks>
/// Tier 3 per <c>testing.md</c>: the sweep deletes with <c>ExecuteDeleteAsync</c>, which the in-memory provider does not implement. The rest of the
/// service's behaviour needs no engine and lives in <see cref="RefreshSessionServiceTests"/>.
/// </remarks>
internal class RefreshSessionPruningTests : SqlServerTestBase
{
    private const int _RETENTION_DAYS = 7;

    private RefreshSessionService _service = null!;

    [SetUp]
    public void Setup()
    {
        RefreshSessionSettings settings = new() { RetentionDays = _RETENTION_DAYS };

        _service = new RefreshSessionService(
            NullLogger<RefreshSessionService>.Instance,
            DbFactory,
            MsOptions.Create(settings));
    }

    [Test, Description("Pruning deletes sessions whose expiry passed longer ago than the retention, which is what stops the table growing with every login.")]
    public async Task Prune_ShouldDeleteSessionsExpiredBeyondRetentionAsync()
    {
        User user = await SeedUserAsync("pruned");

        int staleId = await SeedSessionAsync(user.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(-(_RETENTION_DAYS + 1)));

        int pruned = await _service.PruneExpiredSessionsAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pruned, Is.EqualTo(1));
            Assert.That(await SessionExistsAsync(staleId), Is.False);
        }
    }

    [Test, Description("Pruning keeps a session that expired inside the retention window, so a recent expiry is still there to look at.")]
    public async Task Prune_ShouldKeepSessionsExpiredInsideRetentionAsync()
    {
        User user = await SeedUserAsync("recent");

        int recentlyExpiredId = await SeedSessionAsync(user.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        int pruned = await _service.PruneExpiredSessionsAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pruned, Is.Zero);
            Assert.That(await SessionExistsAsync(recentlyExpiredId), Is.True);
        }
    }

    [Test, Description("Pruning goes by expiry alone, so a session revoked for token reuse survives until its expiry rather than deleting the evidence.")]
    public async Task Prune_ShouldKeepRevokedButUnexpiredSessionsAsync()
    {
        User user = await SeedUserAsync("revoked");

        int revokedId = await SeedSessionAsync(
            user.Id,
            expiresAt: DateTimeOffset.UtcNow.AddDays(14),
            revokedAt: DateTimeOffset.UtcNow,
            revokedReason: RefreshRevocationReason.TokenReuseDetected);

        int pruned = await _service.PruneExpiredSessionsAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pruned, Is.Zero);
            Assert.That(await SessionExistsAsync(revokedId), Is.True);
        }
    }

    [Test, Description("Pruning leaves live sessions alone, so a sweep cannot log anyone out.")]
    public async Task Prune_ShouldLeaveActiveSessionsAloneAsync()
    {
        User user = await SeedUserAsync("active");

        int activeId = await SeedSessionAsync(user.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(14));

        int pruned = await _service.PruneExpiredSessionsAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pruned, Is.Zero);
            Assert.That(await SessionExistsAsync(activeId), Is.True);
        }
    }

    // --- Helpers -----------------------------------------------------------------------------

    private async Task<int> SeedSessionAsync(
        int userId,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt = null,
        RefreshRevocationReason? revokedReason = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        RefreshSession session = new()
        {
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            // Seeded directly rather than through IssueAsync, because these tests need expiries in the past that issuing will not produce.
            TokenHash = Guid.NewGuid().ToString("N"),
            CreatedAt = expiresAt.AddDays(-14),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            RevokedReason = revokedReason,
        };

        context.RefreshSessions.Add(session);
        await context.SaveChangesAsync();
        return session.Id;
    }

    private async Task<bool> SessionExistsAsync(int sessionId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        return await context.RefreshSessions.AnyAsync(s => s.Id == sessionId);
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
