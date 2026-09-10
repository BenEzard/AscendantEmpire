using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Pins which Player Turn <see cref="CampaignTurnService.GetObservationTurnIdAsync"/> stamps on what a Player sees on the Campaign Map.
/// </summary>
/// <remarks>
/// <para>
/// The answer is written into <c>PlayerCampaignMap.FirstSeenOnTurnId</c> and <c>LastSeenOnTurnId</c> on every map read, so a wrong one is silent: the map
/// still draws, and only "when did I last see this" is quietly wrong for ever after.
/// </para>
/// <para>
/// In-memory, per <c>docs/testing.md</c>. The lookup is two plain LINQ queries - no raw SQL, no bulk update, no transaction, and no reliance on the
/// one-open-Turn index - so a container would prove nothing more. Only the rows the queries actually join through are seeded: in-memory enforces no foreign
/// keys, and a Game or Player row would add setup without adding a rule.
/// </para>
/// </remarks>
[TestFixture]
internal class GetObservationTurnIdTests : InMemoryTestBase
{
    // The Game being read, and a second one to show the lookup never strays outside it.
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;

    // The Player opening the map, and the Player whose Turn it may currently be.
    private const int _READER_PLAYER_ID = 10;
    private const int _OTHER_PLAYER_ID = 11;

    private const int _BATTLE_ID = 1;

    private CampaignTurnService _service = null!;

    [SetUp]
    public void CreateService()
    {
        _service = new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory);
    }

    [Test, Description("A Player who has taken several Turns sees with the eyes of their latest one, so a later sighting is never stamped with an earlier Turn.")]
    public async Task ShouldReturnTheReadersMostRecentTurnWhenTheyHaveTakenSeveralAsync()
    {
        int campaignQueueId = await AddQueueAsync(_GAME_ID, TurnQueueType.Campaign);

        await AddTurnAsync(campaignQueueId, _READER_PLAYER_ID, roundNumber: 1, isOpen: false);
        await AddTurnAsync(campaignQueueId, _OTHER_PLAYER_ID, roundNumber: 1, isOpen: false);
        int latestReaderTurnId = await AddTurnAsync(campaignQueueId, _READER_PLAYER_ID, roundNumber: 2, isOpen: true);

        ServiceResult<int> result = await GetObservationTurnIdAsync(_GAME_ID, _READER_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.EqualTo(latestReaderTurnId));
        }
    }

    [Test, Description("Reading out of turn stamps the reader's own last Turn rather than the acting Player's, because the reader's Forces have not moved since.")]
    public async Task ShouldReturnTheReadersOwnTurnWhenAnotherPlayerIsTakingTheirsAsync()
    {
        int campaignQueueId = await AddQueueAsync(_GAME_ID, TurnQueueType.Campaign);

        int readerTurnId = await AddTurnAsync(campaignQueueId, _READER_PLAYER_ID, roundNumber: 1, isOpen: false);

        // Newer, and open - exactly the row a lookup that ignored the reader would pick.
        await AddTurnAsync(campaignQueueId, _OTHER_PLAYER_ID, roundNumber: 1, isOpen: true);

        ServiceResult<int> result = await GetObservationTurnIdAsync(_GAME_ID, _READER_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.EqualTo(readerTurnId), "The acting Player's open Turn says nothing about when the reader saw anything.");
        }
    }

    [Test, Description("A Player who has not acted yet still explores by opening the map, and the lane's open Turn is the only honest answer to when.")]
    public async Task ShouldFallBackToTheOpenTurnWhenTheReaderHasNeverTakenATurnAsync()
    {
        int campaignQueueId = await AddQueueAsync(_GAME_ID, TurnQueueType.Campaign);

        int openTurnId = await AddTurnAsync(campaignQueueId, _OTHER_PLAYER_ID, roundNumber: 1, isOpen: true);

        ServiceResult<int> result = await GetObservationTurnIdAsync(_GAME_ID, _READER_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.EqualTo(openTurnId));
        }
    }

    [Test, Description("A Game in progress always has a Turn, so finding none is a broken invariant and is refused rather than stamped with a placeholder id.")]
    public async Task ShouldFailWhenTheGameHasNoTurnsAsync()
    {
        await AddQueueAsync(_GAME_ID, TurnQueueType.Campaign);

        ServiceResult<int> result = await GetObservationTurnIdAsync(_GAME_ID, _READER_PLAYER_ID);

        Assert.That(result.HasSucceeded, Is.False, "With no Turn to stamp, nothing should be offered as one.");
    }

    [Test, Description("The fallback is scoped to the Game being read, so another Game's open Turn can never be stamped onto this Game's map.")]
    public async Task ShouldFailWhenTheOnlyOpenTurnBelongsToAnotherGameAsync()
    {
        await AddQueueAsync(_GAME_ID, TurnQueueType.Campaign);
        int otherGameQueueId = await AddQueueAsync(_OTHER_GAME_ID, TurnQueueType.Campaign);

        await AddTurnAsync(otherGameQueueId, _OTHER_PLAYER_ID, roundNumber: 1, isOpen: true);

        ServiceResult<int> result = await GetObservationTurnIdAsync(_GAME_ID, _READER_PLAYER_ID);

        Assert.That(result.HasSucceeded, Is.False, "Another Game's Turn is not a Turn of this Game, however open it is.");
    }

    [Test, Description("Battle lanes run their own Rounds outside the campaign order, so only a Campaign Turn can say when something was seen on the Campaign Map.")]
    public async Task ShouldIgnoreBattleLaneTurnsWhenTheReaderHasOneAsync()
    {
        int campaignQueueId = await AddQueueAsync(_GAME_ID, TurnQueueType.Campaign);
        int battleQueueId = await AddQueueAsync(_GAME_ID, TurnQueueType.Battle, battleId: _BATTLE_ID);

        int campaignTurnId = await AddTurnAsync(campaignQueueId, _READER_PLAYER_ID, roundNumber: 1, isOpen: false);

        // Newer than the Campaign Turn, so it would win if the lane were not filtered.
        await AddTurnAsync(battleQueueId, _READER_PLAYER_ID, roundNumber: 1, isOpen: true);

        ServiceResult<int> result = await GetObservationTurnIdAsync(_GAME_ID, _READER_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.EqualTo(campaignTurnId));
        }
    }

    /// <summary>
    /// Runs the lookup under test on a fresh context, as the map read does on its own.
    /// </summary>
    private async Task<ServiceResult<int>> GetObservationTurnIdAsync(int gameId, int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _service.GetObservationTurnIdAsync(context, gameId, playerId);
    }

    /// <summary>
    /// Adds a turn lane and returns its id.
    /// </summary>
    private async Task<int> AddQueueAsync(int gameId, TurnQueueType queueType, int? battleId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var queue = new TurnQueue
        {
            GameId = gameId,
            QueueType = queueType,
            BattleId = battleId,
            CurrentRoundNumber = 1,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        context.TurnQueues.Add(queue);
        await context.SaveChangesAsync();

        return queue.Id;
    }

    /// <summary>
    /// Adds a Player Turn directly and returns its id.
    /// </summary>
    /// <remarks>
    /// Written straight to the table rather than opened through <c>IPlayerTurnService</c>, because the service closes any other open Turn in the lane - and
    /// several of these tests need a closed Turn and an open one side by side, in a set order. Ids are handed out in insertion order, which is what "most
    /// recent" is measured by.
    /// </remarks>
    private async Task<int> AddTurnAsync(int turnQueueId, int playerId, int roundNumber, bool isOpen)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var turn = new PlayerTurn
        {
            TurnQueueId = turnQueueId,
            PlayerId = playerId,
            RoundNumber = roundNumber,
            StartedAt = DateTimeOffset.UtcNow,
            EndedAt = isOpen ? null : DateTimeOffset.UtcNow,
        };

        context.PlayerTurns.Add(turn);
        await context.SaveChangesAsync();

        return turn.Id;
    }
}
