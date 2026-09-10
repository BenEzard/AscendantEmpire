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
/// End-to-end cover for GS-9: a Game created from scratch and then filled by real slot claims must come out the far side playable.
/// </summary>
/// <remarks>
/// <para>
/// This is the test that would have caught the original bug. Setup used to mark a Game in progress while leaving every Turn Queue
/// Participant Pending, so <see cref="ICampaignTurnService.GetCurrentCampaignTurnAsync"/> - which matches only
/// <see cref="TurnQueueParticipantStatus.TakingTurn"/> - found nobody, and the first Turn could never be ended. Each piece looked correct
/// on its own; only running setup and claiming through to the end shows it.
/// </para>
/// <para>
/// Real services throughout except the Turn pipelines, which have nothing registered behind them anyway. Tier 3, because setup and the
/// claim both need a relational engine.
/// </para>
/// </remarks>
[TestFixture]
public class StartOfGameTests : GameSqlServerTestBase
{
    private ICampaignTurnService _campaignTurnService = null!;
    private ClaimPlayerSlotHandler _claimHandler = null!;

    [SetUp]
    public async Task SetUp()
    {
        await SeedGameAsync(2, 2, 2);

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

        _claimHandler = new ClaimPlayerSlotHandler(new NullLogger<ClaimPlayerSlotHandler>(), DbFactory, campaignRoundService);
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

    /// <summary>Claims every slot in the Game, one per user, in Player Id order - the last of which starts the Game.</summary>
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

            ServiceResult<Empty> claimResult = await _claimHandler.Handle(new ClaimPlayerSlotRequest(user.Id, gameId, playerIds[i]), CancellationToken.None);

            Assert.That(claimResult.HasSucceeded, Is.True, $"Claiming slot {playerIds[i]} failed: {string.Join(", ", claimResult.Errors)}");
        }
    }

    [Test, Description("A Game created and then filled by real claims has a current Turn, which is the whole point: without one nobody can ever end a Turn and the Game is unplayable.")]
    public async Task ShouldHaveACurrentTurnWhenEverySlotHasBeenClaimedAsync()
    {
        int gameId = await SetupGameAsync();

        await ClaimEverySlotAsync(gameId);

        ServiceResult<CurrentCampaignTurn> currentTurn = await _campaignTurnService.GetCurrentCampaignTurnAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(currentTurn.HasSucceeded, Is.True, $"No current Turn was found: {string.Join(", ", currentTurn.Errors)}");
            Assert.That(currentTurn.SuccessValue!.TurnOrder, Is.EqualTo(1), "The Player who leads the Game is the one holding the lowest turn order.");
            Assert.That(currentTurn.SuccessValue.CurrentRoundNumber, Is.EqualTo(1), "Play begins on Round 1.");
        }
    }

    [Test, Description("The Game reaches in-progress only once its last slot is claimed, so the lobby state is what the Match Maker sees until then.")]
    public async Task ShouldOnlyReachInProgressOnceTheLastSlotIsClaimedAsync()
    {
        int gameId = await SetupGameAsync();

        GamePlayStatus afterSetup = await ReadPlayStatusAsync(gameId);

        await ClaimEverySlotAsync(gameId);

        GamePlayStatus afterClaims = await ReadPlayStatusAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterSetup, Is.EqualTo(GamePlayStatus.AwaitingPlayers), "Setup finishes with the Game waiting, not playing.");
            Assert.That(afterClaims, Is.EqualTo(GamePlayStatus.InProgress), "Filling the last seat is what starts the Game.");
        }
    }

    [Test, Description("Exactly one Player holds the Turn when the Game starts; two would break the current-turn lookup, and none would make the Game unplayable.")]
    public async Task ShouldGiveTheTurnToExactlyOnePlayerWhenTheGameStartsAsync()
    {
        int gameId = await SetupGameAsync();

        await ClaimEverySlotAsync(gameId);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        int takingTurnCount = await verifyContext.TurnQueueParticipants
            .Where(p => p.TurnQueue.GameId == gameId)
            .Where(p => p.Status == TurnQueueParticipantStatus.TakingTurn)
            .CountAsync();

        Assert.That(takingTurnCount, Is.EqualTo(1));
    }

    private async Task<GamePlayStatus> ReadPlayStatusAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.GameIndex.Where(g => g.Id == gameId).Select(g => g.PlayStatus).FirstAsync();
    }
}
