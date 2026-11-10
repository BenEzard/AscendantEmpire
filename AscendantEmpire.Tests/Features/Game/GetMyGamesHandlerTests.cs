using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.GetMyGames;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Game;

/// <summary>
/// Tests for the Open Game dialog's listing of the caller's own games.
/// </summary>
/// <remarks>
/// <para>
/// The listing replaced an anonymous lobby that returned every in-progress game with one row per active ruler. Two properties matter and neither was true
/// before: a caller only ever sees games they hold an active slot in, and each game is one row naming whoever the campaign queue is waiting on.
/// </para>
/// <para>
/// The current-turn lookup is a correlated subquery that has to behave as a left join. A game between turns, one whose only participant taking a turn sits
/// in a battle lane, and one whose campaign lane has finished all have nothing to name — and all must still be listed. An inner join would drop the
/// caller's own games from their own list, which is why so much of this fixture is about the null case.
/// </para>
/// <para>
/// Runs at the in-memory tier: the handler is a single LINQ query with the labels composed client-side after materialising, so nothing here needs a real
/// database.
/// </para>
/// </remarks>
internal class GetMyGamesHandlerTests : InMemoryTestBase
{
    /// <summary>The account under test. Every assertion is about what this user can and cannot see.</summary>
    private const int _USER_ID = 11;

    /// <summary>A second account, so "somebody else's game" and "somebody else's turn" are real rather than absences.</summary>
    private const int _OTHER_USER_ID = 22;

    private const int _MAP_WIDTH = 40;
    private const int _MAP_HEIGHT = 25;

    // One game per behaviour, so a failure names its own cause rather than pointing at a single overloaded fixture game.
    private const int _OTHERS_TURN_GAME_ID = 4;
    private const int _MY_TURN_GAME_ID = 5;
    private const int _BETWEEN_TURNS_GAME_ID = 6;
    private const int _BATTLE_LANE_GAME_ID = 7;
    private const int _COMPLETED_LANE_GAME_ID = 8;
    private const int _INACTIVE_IN_LANE_GAME_ID = 9;
    private const int _NOT_MY_GAME_ID = 10;
    private const int _ELIMINATED_GAME_ID = 11;
    private const int _AWAITING_PLAYERS_GAME_ID = 12;

    private const int _MY_PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _MY_TURN_PLAYER_ID = 3;

    /// <summary>The caller's campaign offset in the game they are taking their turn in, so the row can be shown to carry their slot and not another's.</summary>
    private const int _MY_TURN_CAMPAIGN_OFFSET_ID = 903;

    private GetMyGamesHandler _handler = null!;

    [SetUp]
    public async Task SeedGamesAsync()
    {
        _handler = new GetMyGamesHandler(DbFactory);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Listed: somebody else is taking their turn, and the game has two active players, so the one-row-per-game rule has something to collapse.
        AddGame(context, _OTHERS_TURN_GAME_ID, "Alpha Campaign");
        AddPlayer(context, _MY_PLAYER_ID, _OTHERS_TURN_GAME_ID, _USER_ID, "Queen", "Mine", "My Faction", campaignOffsetId: 901);
        AddPlayer(context, _OTHER_PLAYER_ID, _OTHERS_TURN_GAME_ID, _OTHER_USER_ID, "King", "Theirs", "Their Faction", campaignOffsetId: 902);
        AddQueue(context, queueId: 1, gameId: _OTHERS_TURN_GAME_ID, TurnQueueType.Campaign);
        AddParticipant(context, participantId: 1, queueId: 1, _MY_PLAYER_ID, TurnQueueParticipantStatus.TurnCompleted_Manual, turnOrder: 1);
        AddParticipant(context, participantId: 2, queueId: 1, _OTHER_PLAYER_ID, TurnQueueParticipantStatus.TakingTurn, turnOrder: 2);

        // Listed: the caller's own turn.
        AddGame(context, _MY_TURN_GAME_ID, "Bravo Campaign");
        AddPlayer(context, _MY_TURN_PLAYER_ID, _MY_TURN_GAME_ID, _USER_ID, "Emperor", "Ours", "Our Faction", _MY_TURN_CAMPAIGN_OFFSET_ID);
        AddQueue(context, queueId: 2, gameId: _MY_TURN_GAME_ID, TurnQueueType.Campaign);
        AddParticipant(context, participantId: 3, queueId: 2, _MY_TURN_PLAYER_ID, TurnQueueParticipantStatus.TakingTurn, turnOrder: 1);

        // Listed with no current turn: the lane exists but nobody has been handed the turn yet.
        AddGame(context, _BETWEEN_TURNS_GAME_ID, "Charlie Campaign");
        AddPlayer(context, playerId: 4, _BETWEEN_TURNS_GAME_ID, _USER_ID, "Duke", "Waiting", "Waiting Faction", campaignOffsetId: 904);
        AddQueue(context, queueId: 3, gameId: _BETWEEN_TURNS_GAME_ID, TurnQueueType.Campaign);
        AddParticipant(context, participantId: 4, queueId: 3, playerId: 4, TurnQueueParticipantStatus.Pending, turnOrder: 1);

        // Listed with no current turn: the only participant taking a turn is doing so in a battle lane, which runs on a different clock.
        AddGame(context, _BATTLE_LANE_GAME_ID, "Delta Campaign");
        AddPlayer(context, playerId: 5, _BATTLE_LANE_GAME_ID, _USER_ID, "Count", "Battling", "Battling Faction", campaignOffsetId: 905);
        AddQueue(context, queueId: 4, gameId: _BATTLE_LANE_GAME_ID, TurnQueueType.Campaign);
        AddParticipant(context, participantId: 5, queueId: 4, playerId: 5, TurnQueueParticipantStatus.Pending, turnOrder: 1);
        AddQueue(context, queueId: 5, gameId: _BATTLE_LANE_GAME_ID, TurnQueueType.Battle, battleId: 77);
        AddParticipant(context, participantId: 6, queueId: 5, playerId: 5, TurnQueueParticipantStatus.TakingTurn, turnOrder: 1);

        // Listed with no current turn: the campaign lane has finished, so its last taking-turn row is history rather than a live turn.
        AddGame(context, _COMPLETED_LANE_GAME_ID, "Echo Campaign");
        AddPlayer(context, playerId: 6, _COMPLETED_LANE_GAME_ID, _USER_ID, "Baron", "Finished", "Finished Faction", campaignOffsetId: 906);
        AddQueue(context, queueId: 6, gameId: _COMPLETED_LANE_GAME_ID, TurnQueueType.Campaign, isCompleted: true);
        AddParticipant(context, participantId: 7, queueId: 6, playerId: 6, TurnQueueParticipantStatus.TakingTurn, turnOrder: 1);

        // Listed with no current turn: a participant who has dropped out of the lane keeps their status but is no longer in the rotation.
        AddGame(context, _INACTIVE_IN_LANE_GAME_ID, "Foxtrot Campaign");
        AddPlayer(context, playerId: 7, _INACTIVE_IN_LANE_GAME_ID, _USER_ID, "Earl", "Dropped", "Dropped Faction", campaignOffsetId: 907);
        AddQueue(context, queueId: 7, gameId: _INACTIVE_IN_LANE_GAME_ID, TurnQueueType.Campaign);
        AddParticipant(context, participantId: 8, queueId: 7, playerId: 7, TurnQueueParticipantStatus.TakingTurn, turnOrder: 1, isActiveInLane: false);

        // Not listed: somebody else's game entirely.
        AddGame(context, _NOT_MY_GAME_ID, "Golf Campaign");
        AddPlayer(context, playerId: 8, _NOT_MY_GAME_ID, _OTHER_USER_ID, "Tsar", "Stranger", "Stranger Faction", campaignOffsetId: 908);

        // Not listed: the caller's slot is no longer active.
        AddGame(context, _ELIMINATED_GAME_ID, "Hotel Campaign");
        AddPlayer(context, playerId: 9, _ELIMINATED_GAME_ID, _USER_ID, "Chief", "Beaten", "Beaten Faction", campaignOffsetId: 909, status: PlayerStatus.Eliminated);

        // Not listed: play has not begun, so there is nothing to open. A game at this stage belongs to the matchmaker, not to this dialog.
        AddGame(context, _AWAITING_PLAYERS_GAME_ID, "India Campaign", playStatus: GamePlayStatus.AwaitingPlayers);
        AddPlayer(context, playerId: 10, _AWAITING_PLAYERS_GAME_ID, _USER_ID, "Regent", "Early", "Early Faction", campaignOffsetId: 910);

        await context.SaveChangesAsync();
    }

    [Test, Description("A caller sees every game they hold an active slot in and no others, which is the whole point of replacing the anonymous lobby.")]
    public async Task ShouldListOnlyTheCallersOwnGamesWhenOtherGamesExistAsync()
    {
        IReadOnlyCollection<MyGame> games = await ListMyGamesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => g.GameId), Does.Not.Contain(_NOT_MY_GAME_ID), "A game the caller holds no slot in is not theirs to open.");
            Assert.That(games.Select(g => g.GameId), Does.Not.Contain(_ELIMINATED_GAME_ID), "An eliminated slot is not an active one.");
            Assert.That(games.Select(g => g.GameId), Does.Not.Contain(_AWAITING_PLAYERS_GAME_ID), "A game still filling its slots cannot be opened.");

            Assert.That(games.Select(g => g.GameId), Is.EquivalentTo(new[]
            {
                _OTHERS_TURN_GAME_ID,
                _MY_TURN_GAME_ID,
                _BETWEEN_TURNS_GAME_ID,
                _BATTLE_LANE_GAME_ID,
                _COMPLETED_LANE_GAME_ID,
                _INACTIVE_IN_LANE_GAME_ID
            }));
        }
    }

    [Test, Description("A game with several active players is one row, not one per ruler, so the dialog lists games rather than seats.")]
    public async Task ShouldReturnOneRowPerGameWhenAGameHasSeveralActivePlayersAsync()
    {
        IReadOnlyCollection<MyGame> games = await ListMyGamesAsync();

        Assert.That(games.Count(g => g.GameId == _OTHERS_TURN_GAME_ID), Is.EqualTo(1));
    }

    [Test, Description("Games come back ordered by name, so the dialog's rows do not reshuffle between refreshes.")]
    public async Task ShouldOrderGamesByNameAsync()
    {
        IReadOnlyCollection<MyGame> games = await ListMyGamesAsync();

        Assert.That(games.Select(g => g.GameName), Is.Ordered);
    }

    [Test, Description("Each row carries the caller's own slot, which is what the client draws the board with once the game is entered.")]
    public async Task ShouldCarryTheCallersOwnSlotRatherThanAnotherPlayersAsync()
    {
        MyGame game = await SingleGameAsync(_MY_TURN_GAME_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.YourPlayerId, Is.EqualTo(_MY_TURN_PLAYER_ID));
            Assert.That(game.YourRulerLabel, Is.EqualTo("Emperor Ours (Our Faction)"));
            Assert.That(game.YourCampaignOffsetId, Is.EqualTo(_MY_TURN_CAMPAIGN_OFFSET_ID));
            Assert.That(game.MapWidth, Is.EqualTo(_MAP_WIDTH));
            Assert.That(game.MapHeight, Is.EqualTo(_MAP_HEIGHT));
        }
    }

    [Test, Description("The current turn names the participant actually taking it, ruler and faction together, which is what the Campaign Turn column shows.")]
    public async Task ShouldNameTheCurrentTurnPlayerWhenSomebodyIsTakingTheirTurnAsync()
    {
        MyGame game = await SingleGameAsync(_OTHERS_TURN_GAME_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.CurrentTurnPlayerId, Is.EqualTo(_OTHER_PLAYER_ID));
            Assert.That(game.CurrentTurnRulerLabel, Is.EqualTo("King Theirs (Their Faction)"));
            Assert.That(game.IsYourTurn, Is.False, "The turn belongs to the other player, not the caller.");
        }
    }

    [Test, Description("The caller's own turn is flagged, so the dialog can mark the row that is waiting on them.")]
    public async Task ShouldFlagYourTurnWhenTheCurrentTurnIsTheCallersOwnAsync()
    {
        MyGame game = await SingleGameAsync(_MY_TURN_GAME_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsYourTurn, Is.True);
            Assert.That(game.CurrentTurnPlayerId, Is.EqualTo(game.YourPlayerId));
        }
    }

    [Test, Description("A game with nobody taking a turn is still listed with a null turn, because dropping the caller's own game is far worse than naming no ruler.")]
    public async Task ShouldReturnTheGameWithNoCurrentTurnWhenNobodyIsTakingOneAsync()
    {
        MyGame game = await SingleGameAsync(_BETWEEN_TURNS_GAME_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.CurrentTurnPlayerId, Is.Null);
            Assert.That(game.CurrentTurnRulerLabel, Is.Null);
            Assert.That(game.IsYourTurn, Is.False, "Nobody's turn is not the caller's turn.");
        }
    }

    [Test, Description("A turn being taken in a battle lane is not the campaign turn, so it must not be reported as one.")]
    public async Task ShouldIgnoreATakingTurnParticipantInABattleLaneAsync()
    {
        MyGame game = await SingleGameAsync(_BATTLE_LANE_GAME_ID);

        Assert.That(game.CurrentTurnPlayerId, Is.Null);
    }

    [Test, Description("A completed campaign lane's last taking-turn row is history rather than a live turn, so it must not be reported.")]
    public async Task ShouldIgnoreATakingTurnParticipantInACompletedLaneAsync()
    {
        MyGame game = await SingleGameAsync(_COMPLETED_LANE_GAME_ID);

        Assert.That(game.CurrentTurnPlayerId, Is.Null);
    }

    [Test, Description("A participant who has dropped out of the lane is out of the rotation whatever status they were left holding.")]
    public async Task ShouldIgnoreATakingTurnParticipantWhoIsNoLongerActiveInTheLaneAsync()
    {
        MyGame game = await SingleGameAsync(_INACTIVE_IN_LANE_GAME_ID);

        Assert.That(game.CurrentTurnPlayerId, Is.Null);
    }

    private async Task<IReadOnlyCollection<MyGame>> ListMyGamesAsync()
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<MyGame>> result =
            await _handler.Handle(new GetMyGamesRequest(_USER_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private async Task<MyGame> SingleGameAsync(int gameId)
    {
        IReadOnlyCollection<MyGame> games = await ListMyGamesAsync();

        return games.Single(g => g.GameId == gameId);
    }

    private static void AddGame(
        AscendantEmpireContext context,
        int gameId,
        string gameName,
        GamePlayStatus playStatus = GamePlayStatus.InProgress)
    {
        context.GameIndex.Add(new GameIndex
        {
            Id = gameId,
            CampaignMapIndexId = 1,
            GameName = gameName,
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = playStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30,
            MapWidth = _MAP_WIDTH,
            MapHeight = _MAP_HEIGHT,
            CampaignMapOffsetId = gameId * 1_000,
        });
    }

    private static void AddPlayer(
        AscendantEmpireContext context,
        int playerId,
        int gameId,
        int userId,
        string rulerTitle,
        string rulerName,
        string factionName,
        int campaignOffsetId,
        PlayerStatus status = PlayerStatus.Active)
    {
        context.Players.Add(new Player
        {
            Id = playerId,
            GameId = gameId,
            UserId = userId,
            RulerTitle = rulerTitle,
            RulerName = rulerName,
            FactionName = factionName,
            PeopleName = "People",
            Demonym = "Person",
            Language = "Language",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = status,
            CampaignOffsetId = campaignOffsetId,
        });
    }

    private static void AddQueue(
        AscendantEmpireContext context,
        int queueId,
        int gameId,
        TurnQueueType queueType,
        int? battleId = null,
        bool isCompleted = false)
    {
        context.TurnQueues.Add(new TurnQueue
        {
            Id = queueId,
            GameId = gameId,
            QueueType = queueType,
            BattleId = battleId,
            CurrentRoundNumber = 1,
            IsCompleted = isCompleted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
    }

    private static void AddParticipant(
        AscendantEmpireContext context,
        int participantId,
        int queueId,
        int playerId,
        TurnQueueParticipantStatus status,
        int turnOrder,
        bool isActiveInLane = true)
    {
        context.TurnQueueParticipants.Add(new TurnQueueParticipant
        {
            Id = participantId,
            TurnQueueId = queueId,
            PlayerId = playerId,
            TurnOrder = turnOrder,
            Status = status,
            IsActiveInLane = isActiveInLane,
        });
    }
}
