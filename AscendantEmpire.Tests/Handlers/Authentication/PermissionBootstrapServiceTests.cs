using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AscendantEmpire.Tests.Handlers.Authentication;

/// <summary>
/// Covers the startup grant that breaks the permissions chicken-and-egg: nobody can reach the System panel to grant permissions until somebody already has
/// one.
/// </summary>
/// <remarks>
/// The service only reads and writes ordinary rows, so the in-memory tier suffices. The one case that needs no database at all lives in
/// <see cref="PermissionBootstrapResilienceTests"/> so it doesn't stand one up for nothing.
/// </remarks>
internal class PermissionBootstrapServiceTests : InMemoryTestBase
{
    private const string AdminUsername = "the-administrator";

    /// <summary>Adds an account and returns its id.</summary>
    private async Task<int> SeedUserAsync(string username)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        User user = new()
        {
            Username = username,
            PasswordHash = "not-a-real-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            InactiveReasonType = InactiveReasonType.Active,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>Grants one permission directly, bypassing the handler.</summary>
    private async Task GrantAsync(int userId, PermissionType permission)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UserPermissions.Add(new UserPermission
        {
            UserId = userId,
            Permission = permission,
            GrantedAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Runs the service with the given configured administrator.</summary>
    private async Task RunBootstrapAsync(string adminUsername)
    {
        PermissionBootstrapService service = new(
            NullLogger<PermissionBootstrapService>.Instance,
            Options.Create(new BootstrapSettings { AdminUsername = adminUsername }),
            DbFactory);

        await service.StartAsync(CancellationToken.None);
    }

    private async Task<List<PermissionType>> ReadPermissionsAsync(int userId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UserPermissions
            .Where(permission => permission.UserId == userId)
            .Select(permission => permission.Permission)
            .ToListAsync();
    }

    [Test, Description("With no administrator anywhere, the configured account is granted every permission.")]
    public async Task Bootstrap_GrantsEveryPermission_WhenNobodyHoldsSystemPanel()
    {
        int userId = await SeedUserAsync(AdminUsername);

        await RunBootstrapAsync(AdminUsername);

        Assert.That(await ReadPermissionsAsync(userId), Is.EquivalentTo(Enum.GetValues<PermissionType>()));
    }

    [Test, Description("Once any account holds SystemPanel the grant stops running, so it can't quietly re-promote a demoted user.")]
    public async Task Bootstrap_DoesNothing_WhenSomebodyAlreadyHoldsSystemPanel()
    {
        int adminId = await SeedUserAsync(AdminUsername);
        int otherId = await SeedUserAsync("someone-else");
        await GrantAsync(otherId, PermissionType.SystemPanel);

        await RunBootstrapAsync(AdminUsername);

        Assert.That(await ReadPermissionsAsync(adminId), Is.Empty);
    }

    [Test, Description("A blank setting disables the grant entirely — the expected steady state once a real administrator exists.")]
    public async Task Bootstrap_DoesNothing_WhenNoAdminUsernameIsConfigured()
    {
        int userId = await SeedUserAsync(AdminUsername);

        await RunBootstrapAsync(string.Empty);

        Assert.That(await ReadPermissionsAsync(userId), Is.Empty);
    }

    [Test, Description("A configured username that matches no account is skipped rather than throwing.")]
    public async Task Bootstrap_DoesNothing_WhenTheConfiguredUserDoesNotExist()
    {
        int userId = await SeedUserAsync("somebody-else-entirely");

        await RunBootstrapAsync(AdminUsername);

        Assert.That(await ReadPermissionsAsync(userId), Is.Empty);
    }

    [Test, Description("Permissions the configured account already holds are not re-inserted, so the unique index can't be violated.")]
    public async Task Bootstrap_OnlyAddsTheMissingPermissions()
    {
        int userId = await SeedUserAsync(AdminUsername);
        await GrantAsync(userId, PermissionType.CreateGame);

        await RunBootstrapAsync(AdminUsername);

        List<PermissionType> held = await ReadPermissionsAsync(userId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(held, Is.EquivalentTo(Enum.GetValues<PermissionType>()));
            Assert.That(held, Is.Unique);
        }
    }
}

/// <summary>
/// Covers the promise that a failing bootstrap never stops the host from starting.
/// </summary>
/// <remarks>
/// No database tier at all: the whole point is that the database is unreachable, so this deliberately does not inherit <see cref="InMemoryTestBase"/>.
/// </remarks>
internal class PermissionBootstrapResilienceTests
{
    [Test, Description("An unreachable database is logged and swallowed, because the API can serve traffic without the grant having run.")]
    public async Task Bootstrap_DoesNotThrow_WhenTheDatabaseIsUnreachable()
    {
        IDbContextFactory<AscendantEmpireContext> failingFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        failingFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the database is not reachable"));

        CapturingLogger<PermissionBootstrapService> logger = new();

        PermissionBootstrapService service = new(
            logger,
            Options.Create(new BootstrapSettings { AdminUsername = "the-administrator" }),
            failingFactory);

        Assert.DoesNotThrowAsync(async () => await service.StartAsync(CancellationToken.None));

        // Swallowed, but not silently: the failure has to be visible to whoever is wondering why nobody can log in.
        Assert.That(logger.LastException, Is.Not.Null);

        await Task.CompletedTask;
    }
}
