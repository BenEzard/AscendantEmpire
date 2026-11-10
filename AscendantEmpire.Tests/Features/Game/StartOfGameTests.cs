using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Game;

/// <summary>
/// End-to-end cover for GS-9 and GS-11: a Game created from scratch, then filled by real slot claims and real player definitions, must come out the
/// far side playable - and must not become playable any sooner.
/// </summary>
/// <remarks>
/// <para>
/// This is the test that would have caught the original bug. Setup used to mark a Game in progress while leaving every Turn Queue
/// Participant Pending, so <see cref="ICampaignTurnService.GetCurrentCampaignTurnAsync"/> - which matches only
/// <see cref="TurnQueueParticipantStatus.TakingTurn"/> - found nobody, and the first Turn could never be ended. Each piece looked correct
/// on its own; only running setup and claiming through to the end shows it.
/// </para>
/// <para>
/// It now also pins where the start gate sits. Claiming the last seat leaves the Game waiting; the last player to define themselves is what starts
/// it. Those two halves are separate steps here precisely so a regression that moved the trigger back onto the claim would fail rather than pass.
/// </para>
/// <para>
/// Real services throughout except the Turn pipelines, which have nothing registered behind them anyway. Tier 3, because setup, the claim and the
/// definition all need a relational engine.
/// </para>
/// </remarks>
[TestFixture]
public class StartOfGameTests : GameSqlServerTestBase
{
    private ICampaignTurnService _campaignTurnService = null!;
    private ClaimPlayerSlotHandler _claimHandler = null!;
    private UpdatePlayerDefinitionHandler _defineHandler = null!;

    /// <summary>The users created for each slot, in the Player Id order the slots were claimed in.</summary>
    private readonly List<int> _userIdsBySlot = [];

    [SetUp]
    public async Task SetUp()
    {
        await SeedGameAsync(2, 2, 2);

        _userIdsBySlot.Clear();

        var startCampaignRoundPipeline = Substitute.For<ITurnServicePipeline<IStartCampaignRoundProcess>>();
        var startCampaignTurnPipeline = Substitute.For<ITurnServicePipeline<IStartCampaignTurnProcess>>();

        startCampaignRoundPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());
        startCampaignTurnPipeline.ProcessPipelineAsync(Arg.Any<TurnPipelineContextData>()).Returns(ServiceResultBase.Succeed());

        _campaignTurnService = new CampaignTurnService(new NullLogger<CampaignTurnService>(), DbFactory);

        var campaignRoundService = new CampaignRoundService(
            new NullLogger<CampaignRoundService>(),
            _campaignTurnService,
            new PlayerTurnService(new NullLogger<PlayerTurnService>(), DbFactory),
            startCampaignRoundPipeline,
            startCampaignTurnPipeline);

        _claimHandler = new ClaimPlayerSlotHandler(new NullLogger<ClaimPlayerSlotHandler>(), DbFactory);
        _defineHandler = new UpdatePlayerDefinitionHandler(new NullLogger<UpdatePlayerDefinitionHandler>(), DbFactory, campaignRoundService);
    }

    /// <summary>Runs the real game setup and returns the new Game's Id.</summary>
    private async Task<int> SetupGameAsync()
    {
        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: "Start Of Game", NumberOfPlayers: _numberOfPlayers, BattleMapSize: 50, GenerationSeed: null);

        ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True,
            $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        return result.SuccessValue;
    }

    /// <summary>Claims every slot in the Game, one per user, in Player Id order. On its own this no longer starts the Game.</summary>
    private async Task ClaimEverySlotAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        List<int> playerIds = [.. context.Players
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)];

        for (int i = 0; i < playerIds.Count; i++)
        {
            var user = new User
            {
                Username = $"player-{gameId}-{i}",
                PasswordHash = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            _userIdsBySlot.Add(user.Id);

            ServiceResult<Empty> claimResult = await _claimHandler.Handle(new ClaimPlayerSlotRequest(user.Id, gameId, playerIds[i]), CancellationToken.None);

            Assert.That(claimResult.HasSucceeded, Is.True, $"Claiming slot {playerIds[i]} failed: {string.Join(", ", claimResult.Errors)}");
        }
    }

    /// <summary>
    /// Defines every claimed player, in the order their seats were taken - the last of which starts the Game.
    /// </summary>
    /// <remarks>
    /// Every value that has to be unique within a Game is derived from the slot's position, because a repeat would be refused rather than quietly
    /// overwritten and the failure would read as a start-gate bug rather than the clash it is.
    /// </remarks>
    private async Task DefineEveryPlayerAsync(int gameId)
    {
        for (int i = 0; i < _userIdsBySlot.Count; i++)
        {
            ServiceResult<Empty> defineResult = await _defineHandler.Handle(BuildDefinition(_userIdsBySlot[i], gameId, i + 1), CancellationToken.None);

            Assert.That(defineResult.HasSucceeded, Is.True, $"Defining player {i + 1} failed: {string.Join(", ", defineResult.Errors)}");
        }
    }

    /// <summary>A complete, unique-within-the-game definition for the <paramref name="slotNumber"/>th player.</summary>
    private static UpdatePlayerDefinitionRequest BuildDefinition(int userId, int gameId, int slotNumber)
    {
        return new UpdatePlayerDefinitionRequest(
            UserId: userId,
            GameId: gameId,
            RulerTitle: "Empress",
            RulerName: $"Ruler {slotNumber}",
            RulerGender: RulerGender.Female,
            FactionName: $"Faction {slotNumber}",
            FactionColour: $"#c800{slotNumber:d2}",
            PeopleName: $"People {slotNumber}",
            Demonym: $"Person {slotNumber}",
            Language: "Shared Tongue",
            RulerPortraitPath: RulerPortraitPaths.Female[slotNumber - 1]);
    }

    [Test, Description("A Game created and then filled by real claims and definitions has a current Turn, which is the whole point: without one nobody can ever end a Turn and the Game is unplayable.")]
    public async Task ShouldHaveACurrentTurnWhenEveryPlayerHasBeenDefinedAsync()
    {
        int gameId = await SetupGameAsync();

        await ClaimEverySlotAsync(gameId);
        await DefineEveryPlayerAsync(gameId);

        ServiceResult<CurrentCampaignTurn> currentTurn = await _campaignTurnService.GetCurrentCampaignTurnAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentTurn.HasSucceeded, Is.True, $"No current Turn was found: {string.Join(", ", currentTurn.Errors)}");
            Assert.That(currentTurn.SuccessValue!.TurnOrder, Is.EqualTo(1), "The Player who leads the Game is the one holding the lowest turn order.");
            Assert.That(currentTurn.SuccessValue.CurrentRoundNumber, Is.EqualTo(1), "Play begins on Round 1.");
        }
    }

    [Test, Description("The Game reaches in-progress only once its last player is defined, so a full lobby of players still filling the form in is what the Match Maker sees until then.")]
    public async Task ShouldOnlyReachInProgressOnceTheLastPlayerIsDefinedAsync()
    {
        int gameId = await SetupGameAsync();

        GamePlayStatus afterSetup = await ReadPlayStatusAsync(gameId);

        await ClaimEverySlotAsync(gameId);

        GamePlayStatus afterClaims = await ReadPlayStatusAsync(gameId);

        await DefineEveryPlayerAsync(gameId);

        GamePlayStatus afterDefinitions = await ReadPlayStatusAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterSetup, Is.EqualTo(GamePlayStatus.AwaitingPlayers), "Setup finishes with the Game waiting, not playing.");
            Assert.That(afterClaims, Is.EqualTo(GamePlayStatus.AwaitingPlayers), "A full lobby is not a started Game: nobody has said who they are yet.");
            Assert.That(afterDefinitions, Is.EqualTo(GamePlayStatus.InProgress), "The last definition to land is what starts the Game.");
        }
    }

    [Test, Description("A Game with one player still undefined stays in the lobby, so a seat somebody claimed and walked away from cannot drag the Game into play.")]
    public async Task ShouldStayWaitingWhileOnePlayerIsStillUndefinedAsync()
    {
        int gameId = await SetupGameAsync();

        await ClaimEverySlotAsync(gameId);

        // Everybody but the last seat. With _numberOfPlayers at 2 that is one definition short of the gate.
        for (int i = 0; i < _userIdsBySlot.Count - 1; i++)
        {
            ServiceResult<Empty> defineResult = await _defineHandler.Handle(BuildDefinition(_userIdsBySlot[i], gameId, i + 1), CancellationToken.None);

            Assert.That(defineResult.HasSucceeded, Is.True, $"Defining player {i + 1} failed: {string.Join(", ", defineResult.Errors)}");
        }

        GamePlayStatus playStatus = await ReadPlayStatusAsync(gameId);

        Assert.That(playStatus, Is.EqualTo(GamePlayStatus.AwaitingPlayers));
    }

    [Test, Description("Exactly one Player holds the Turn when the Game starts; two would break the current-turn lookup, and none would make the Game unplayable.")]
    public async Task ShouldGiveTheTurnToExactlyOnePlayerWhenTheGameStartsAsync()
    {
        int gameId = await SetupGameAsync();

        await ClaimEverySlotAsync(gameId);
        await DefineEveryPlayerAsync(gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        int takingTurnCount = await verifyContext.TurnQueueParticipants
            .Where(p => p.TurnQueue.GameId == gameId)
            .Where(p => p.Status == TurnQueueParticipantStatus.TakingTurn)
            .CountAsync();

        Assert.That(takingTurnCount, Is.EqualTo(1));
    }

    [Test, Description("Re-saving a definition after play has begun does not restart the Game, so an edit of the one field that stays editable is harmless.")]
    public async Task ShouldNotRestartTheGameWhenADefinitionIsResavedAsync()
    {
        int gameId = await SetupGameAsync();

        await ClaimEverySlotAsync(gameId);
        await DefineEveryPlayerAsync(gameId);

        int roundBefore = await ReadCurrentRoundNumberAsync(gameId);

        // The locked path: only the title is read, and StartGameAsync is gated on AwaitingPlayers so it cannot fire again.
        ServiceResult<Empty> result = await _defineHandler.Handle(BuildDefinition(_userIdsBySlot[0], gameId, slotNumber: 1), CancellationToken.None);

        int roundAfter = await ReadCurrentRoundNumberAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(await ReadPlayStatusAsync(gameId), Is.EqualTo(GamePlayStatus.InProgress));
            Assert.That(roundAfter, Is.EqualTo(roundBefore), "The Round must not be opened a second time.");
        }
    }

    private async Task<GamePlayStatus> ReadPlayStatusAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.GameIndex.Where(g => g.Id == gameId).Select(g => g.PlayStatus).FirstAsync();
    }

    private async Task<int> ReadCurrentRoundNumberAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.TurnQueues
            .Where(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null)
            .Select(q => q.CurrentRoundNumber)
            .FirstAsync();
    }
}
