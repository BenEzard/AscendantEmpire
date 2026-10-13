using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Handlers;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.Authentication;

/// <summary>
/// Covers granting and revoking a user's permissions, and reading them back the way the token-minting paths do.
/// </summary>
/// <remarks>
/// The handler sticks to plain EF LINQ, <c>Add</c>/<c>RemoveRange</c> and <c>SaveChanges</c>, and never leans on the unique index to enforce anything — it
/// works out what to insert in code — so the in-memory tier suffices.
/// </remarks>
internal class UserPermissionHandlerTests : InMemoryTestBase
{
    private int _userId;

    /// <summary>Seeds a single account and captures its id in <see cref="_userId"/>.</summary>
    [SetUp]
    public async Task SeedUserAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        User user = new()
        {
            Username = "permission-tests",
            PasswordHash = "not-a-real-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            InactiveReasonType = InactiveReasonType.Active,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        _userId = user.Id;
    }

    /// <summary>Runs the handler against a fresh context, so nothing is served out of a previous change tracker.</summary>
    private async Task<ServiceResultBase> SetPermissionsAsync(int userId, params PermissionType[] permissions)
    {
        SetUserPermissionsHandler handler = new(NullLogger<SetUserPermissionsHandler>.Instance, DbFactory);

        return await handler.Handle(new SetUserPermissionsRequest(userId, permissions), CancellationToken.None);
    }

    /// <summary>Reads the permissions currently held, through the same query the login and refresh paths use.</summary>
    private async Task<IReadOnlyCollection<PermissionType>> ReadPermissionsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.GetUserPermissionsAsync(_userId, CancellationToken.None);
    }

    [Test, Description("Granting permissions to a user with none persists exactly the requested set.")]
    public async Task SetPermissions_GrantsTheRequestedSet_WhenTheUserHasNone()
    {
        ServiceResultBase result = await SetPermissionsAsync(_userId, PermissionType.CreateGame, PermissionType.MapEditor);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.SuccessNoContent));
        Assert.That(await ReadPermissionsAsync(), Is.EquivalentTo(new[] { PermissionType.CreateGame, PermissionType.MapEditor }));
    }

    [Test, Description("The request is the complete desired state: permissions it omits are revoked, and ones it adds are granted.")]
    public async Task SetPermissions_ReplacesTheWholeSet_RatherThanMergingIntoIt()
    {
        await SetPermissionsAsync(_userId, PermissionType.CreateGame, PermissionType.JoinGame);

        await SetPermissionsAsync(_userId, PermissionType.JoinGame, PermissionType.SystemPanel);

        Assert.That(await ReadPermissionsAsync(), Is.EquivalentTo(new[] { PermissionType.JoinGame, PermissionType.SystemPanel }));
    }

    [Test, Description("An empty request is meaningful and strips every permission the user holds.")]
    public async Task SetPermissions_RevokesEverything_WhenGivenAnEmptySet()
    {
        await SetPermissionsAsync(_userId, PermissionType.SystemPanel, PermissionType.EntitySetupTool);

        ServiceResultBase result = await SetPermissionsAsync(_userId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.SuccessNoContent));
        Assert.That(await ReadPermissionsAsync(), Is.Empty);
    }

    [Test, Description("Re-saving an unchanged set leaves the existing rows untouched, so a grant keeps its original id and timestamp.")]
    public async Task SetPermissions_LeavesUnchangedGrantsInPlace()
    {
        await SetPermissionsAsync(_userId, PermissionType.CreateGame, PermissionType.JoinGame);

        List<UserPermission> before = await ReadRowsAsync();

        await SetPermissionsAsync(_userId, PermissionType.CreateGame, PermissionType.JoinGame);

        List<UserPermission> after = await ReadRowsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Select(row => row.Id), Is.EquivalentTo(before.Select(row => row.Id)));
            Assert.That(after.Select(row => row.GrantedAt), Is.EquivalentTo(before.Select(row => row.GrantedAt)));
        }
    }

    [Test, Description("A request naming a user that does not exist is a 404 rather than a silent no-op.")]
    public async Task SetPermissions_ReturnsNotFound_ForAnUnknownUser()
    {
        ServiceResultBase result = await SetPermissionsAsync(_userId + 1000, PermissionType.CreateGame);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A newly registered account can play immediately but reaches no developer tool.")]
    public async Task RegisterUser_GrantsOnlyTheTwoPlayerPermissions()
    {
        RegisterUserHandler handler = new(
            NullLogger<RegisterUserHandler>.Instance,
            DbFactory,
            new Microsoft.AspNetCore.Identity.PasswordHasher<User>());

        ServiceResult<Application.Features.Authentication.Classes.AuthenticatedUser> result =
            await handler.Handle(new RegisterUserRequest("new-player", "a-long-enough-password", null), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        IReadOnlyCollection<PermissionType> granted = await context.GetUserPermissionsAsync(result.SuccessValue!.UserId, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Both the persisted rows and the returned identity must agree, since the latter is what goes into the token.
            Assert.That(granted, Is.EquivalentTo(new[] { PermissionType.CreateGame, PermissionType.JoinGame }));
            Assert.That(result.SuccessValue!.Permissions, Is.EquivalentTo(new[] { PermissionType.CreateGame, PermissionType.JoinGame }));
        }
    }

    [Test, Description("The users list carries each account's permissions, so the System panel's dialog opens on data it already has.")]
    public async Task GetUsers_ReturnsEachUsersPermissions()
    {
        await SetPermissionsAsync(_userId, PermissionType.SystemPanel, PermissionType.MapEditor);

        // A second account with no grants at all, to prove the empty case comes back empty rather than missing.
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.Users.Add(new User
            {
                Username = "no-permissions",
                PasswordHash = "not-a-real-hash",
                CreatedAt = DateTimeOffset.UtcNow,
                InactiveReasonType = InactiveReasonType.Active,
            });
            await context.SaveChangesAsync();
        }

        GetUsersHandler handler = new(DbFactory);

        ServiceResult<IReadOnlyCollection<BackendContracts.Authentication.GetUsers.AuthUser>> result =
            await handler.Handle(new GetUsersRequest(), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));

        BackendContracts.Authentication.GetUsers.AuthUser[] users = [.. result.SuccessValue!];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(users.Single(user => user.Id == _userId).Permissions,
                Is.EquivalentTo(new[] { PermissionType.SystemPanel, PermissionType.MapEditor }));
            Assert.That(users.Single(user => user.Username == "no-permissions").Permissions, Is.Empty);
        }
    }

    private async Task<List<UserPermission>> ReadRowsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UserPermissions
            .AsNoTracking()
            .Where(permission => permission.UserId == _userId)
            .OrderBy(permission => permission.Permission)
            .ToListAsync();
    }
}
