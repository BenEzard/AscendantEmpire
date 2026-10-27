using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;

namespace AscendantEmpire.Tests.Handlers.Authentication;

/// <summary>
/// Covers the register / login / enter-game handlers using an in-memory database. These
/// handlers don't rely on SQL-Server-only features (the duplicate-username check is done in
/// code, not via the unique index), so the fast in-memory provider is sufficient.
/// </summary>
internal class AuthenticationHandlerTests
{
    private static readonly ServiceProvider EfServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(AuthenticationHandlerTests), DbRoot)
        .UseInternalServiceProvider(EfServiceProvider)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private IPasswordHasher<User> _passwordHasher = null!;

    [SetUp]
    public async Task Setup()
    {
        await using (var setupContext = new AscendantEmpireContext(DbOptions))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        _dbFactory = new TestDbContextFactory<AscendantEmpireContext>(DbOptions);
        _passwordHasher = new PasswordHasher<User>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => EfServiceProvider.Dispose();

    // --- Register ----------------------------------------------------------------------------

    [Test, Description("Registering an available username creates a user whose password is stored hashed, not in plaintext.")]
    public async Task Register_ShouldCreateUserWithHashedPasswordAsync()
    {
        var handler = new RegisterUserHandler(NullLogger<RegisterUserHandler>.Instance, _dbFactory, _passwordHasher);

        var result = await handler.Handle(new RegisterUserRequest("alice", "password123", null), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created));
            Assert.That(result.SuccessValue!.Username, Is.EqualTo("alice"));
        }

        await using var context = new AscendantEmpireContext(DbOptions);
        User stored = await context.Users.SingleAsync(u => u.Username == "alice");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.PasswordHash, Is.Not.Empty);
            Assert.That(stored.PasswordHash, Is.Not.EqualTo("password123"));
            Assert.That(stored.InactiveAt, Is.Null);
            Assert.That(stored.InactiveReasonType, Is.EqualTo(InactiveReasonType.Active));
        }
    }

    [Test, Description("Getting auth users returns the projected user records including inactivity metadata.")]
    public async Task GetUsers_ShouldReturnProjectedAuthUsersAsync()
    {
        DateTimeOffset inactiveAt = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);

        User activeUser = await SeedUserAsync("alice", "password123");
        User inactiveUser = await SeedUserAsync("banned-user", "password123", inactiveAt, InactiveReasonType.Banned, "banned@example.com");

        var handler = new GetUsersHandler(_dbFactory);

        var result = await handler.Handle(new GetUsersRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Not.Null);
            Assert.That(result.SuccessValue!.Select(user => user.Id), Is.EqualTo(new[] { activeUser.Id, inactiveUser.Id }));
        }

        var users = result.SuccessValue!.ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(users[0].Username, Is.EqualTo("alice"));
            Assert.That(users[0].Email, Is.Null);
            Assert.That(users[0].InactiveAt, Is.Null);
            Assert.That(users[0].InactiveReasonType, Is.EqualTo(InactiveReasonType.Active));

            Assert.That(users[1].Username, Is.EqualTo("banned-user"));
            Assert.That(users[1].Email, Is.EqualTo("banned@example.com"));
            Assert.That(users[1].InactiveAt, Is.EqualTo(inactiveAt));
            Assert.That(users[1].InactiveReasonType, Is.EqualTo(InactiveReasonType.Banned));
        }
    }

    [Test, Description("Setting a user inactive stores the requested non-active reason and records the inactive timestamp.")]
    public async Task SetUserInactive_ShouldSetReasonAndTimestampAsync()
    {
        User user = await SeedUserAsync("quitter", "password123");

        var handler = new SetUserInactiveHandler(NullLogger<SetUserInactiveHandler>.Instance, _dbFactory);

        var result = await handler.Handle(new SetUserInactiveRequest(user.Id, InactiveReasonType.Quit), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.SuccessNoContent));
        }

        await using var context = new AscendantEmpireContext(DbOptions);
        User stored = await context.Users.SingleAsync(u => u.Id == user.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.InactiveReasonType, Is.EqualTo(InactiveReasonType.Quit));
            Assert.That(stored.InactiveAt, Is.Not.Null);
        }
    }

    [Test, Description("Setting a user back to Active clears the inactive timestamp.")]
    public async Task SetUserInactive_ShouldClearTimestampWhenReactivatedAsync()
    {
        DateTimeOffset inactiveAt = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
        User user = await SeedUserAsync("reactivate-me", "password123", inactiveAt, InactiveReasonType.Banned);

        var handler = new SetUserInactiveHandler(NullLogger<SetUserInactiveHandler>.Instance, _dbFactory);

        var result = await handler.Handle(new SetUserInactiveRequest(user.Id, InactiveReasonType.Active), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.SuccessNoContent));
        }

        await using var context = new AscendantEmpireContext(DbOptions);
        User stored = await context.Users.SingleAsync(u => u.Id == user.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.InactiveReasonType, Is.EqualTo(InactiveReasonType.Active));
            Assert.That(stored.InactiveAt, Is.Null);
        }
    }

    [Test, Description("Setting inactivity for an unknown user returns NotFound.")]
    public async Task SetUserInactive_ShouldReturnNotFoundForUnknownUserAsync()
    {
        var handler = new SetUserInactiveHandler(NullLogger<SetUserInactiveHandler>.Instance, _dbFactory);

        var result = await handler.Handle(new SetUserInactiveRequest(999, InactiveReasonType.Deleted), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [Test, Description("Registering an already-taken username is rejected with a Conflict.")]
    public async Task Register_ShouldRejectDuplicateUsernameAsync()
    {
        await SeedUserAsync("bob", "secretpass");

        var handler = new RegisterUserHandler(NullLogger<RegisterUserHandler>.Instance, _dbFactory, _passwordHasher);

        var result = await handler.Handle(new RegisterUserRequest("bob", "differentpass", null), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
        }
    }

    // --- Login -------------------------------------------------------------------------------

    [Test, Description("Logging in with the correct password returns the user's identity.")]
    public async Task Login_ShouldSucceedWithCorrectPasswordAsync()
    {
        User user = await SeedUserAsync("carol", "correct-horse");

        var handler = new LoginHandler(NullLogger<LoginHandler>.Instance, _dbFactory, _passwordHasher);

        var result = await handler.Handle(new LoginRequest("carol", "correct-horse"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.UserId, Is.EqualTo(user.Id));
            Assert.That(result.SuccessValue!.Username, Is.EqualTo("carol"));
        }
    }

    [Test, Description("Logging in with a wrong password is forbidden.")]
    public async Task Login_ShouldFailWithWrongPasswordAsync()
    {
        await SeedUserAsync("dave", "the-right-one");

        var handler = new LoginHandler(NullLogger<LoginHandler>.Instance, _dbFactory, _passwordHasher);

        var result = await handler.Handle(new LoginRequest("dave", "the-wrong-one"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
        }
    }

    [Test, Description("Logging in as an unknown user is forbidden (and indistinguishable from a wrong password).")]
    public async Task Login_ShouldFailForUnknownUserAsync()
    {
        var handler = new LoginHandler(NullLogger<LoginHandler>.Instance, _dbFactory, _passwordHasher);

        var result = await handler.Handle(new LoginRequest("nobody", "whatever"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
        }
    }

    // --- Enter game --------------------------------------------------------------------------

    [Test, Description("Entering a game the user has a player in returns the game and player context.")]
    public async Task EnterGame_ShouldReturnContextWhenUserHasPlayerAsync()
    {
        User user = await SeedUserAsync("erin", "passphrase1");
        await SeedGameAsync(gameId: 7);
        Player player = await SeedPlayerAsync(playerId: 70, gameId: 7, userId: user.Id);

        var handler = new EnterGameHandler(NullLogger<EnterGameHandler>.Instance, _dbFactory);

        var result = await handler.Handle(new EnterGameRequest(user.Id, 7), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.UserId, Is.EqualTo(user.Id));
            Assert.That(result.SuccessValue!.Username, Is.EqualTo("erin"));
            Assert.That(result.SuccessValue!.GameId, Is.EqualTo(7));
            Assert.That(result.SuccessValue!.PlayerId, Is.EqualTo(player.Id));
        }
    }

    [Test, Description("Entering a game the user has no player in is forbidden.")]
    public async Task EnterGame_ShouldForbidWhenUserHasNoPlayerAsync()
    {
        User user = await SeedUserAsync("frank", "passphrase2");
        await SeedGameAsync(gameId: 8);
        // A player exists in the game, but it belongs to someone else (UserId null).
        await SeedPlayerAsync(playerId: 80, gameId: 8, userId: null);

        var handler = new EnterGameHandler(NullLogger<EnterGameHandler>.Instance, _dbFactory);

        var result = await handler.Handle(new EnterGameRequest(user.Id, 8), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Forbidden));
        }
    }

    // --- Seeding helpers ---------------------------------------------------------------------

    private async Task<User> SeedUserAsync(
        string username,
        string password,
        DateTimeOffset? inactiveAt = null,
        InactiveReasonType inactiveReasonType = InactiveReasonType.Active,
        string? email = null)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        User user = new()
        {
            Username = username,
            Email = email,
            PasswordHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            InactiveAt = inactiveAt,
            InactiveReasonType = inactiveReasonType,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task SeedGameAsync(int gameId)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        context.GameIndex.Add(new GameIndex
        {
            Id = gameId,
            GameName = $"Game {gameId}",
            NumberOfPlayers = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            GenerationSeed = 0,
            BattleMapSize = 50,
        });
        await context.SaveChangesAsync();
    }

    private async Task<Player> SeedPlayerAsync(int playerId, int gameId, int? userId)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        Player player = new()
        {
            Id = playerId,
            GameId = gameId,
            UserId = userId,
            Status = PlayerStatus.Active,
            RulerName = $"Ruler {playerId}",
            RulerTitle = $"Title {playerId}",
            FactionName = $"Faction {playerId}",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/01_western_king.png",
            CampaignOffsetId = playerId,
        };

        context.Players.Add(player);
        await context.SaveChangesAsync();
        return player;
    }
}
