using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Battles.Services;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Enums.Battle;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Battles.Services;

/// <summary>
/// Tests for what <see cref="BattleLifecycleWriter"/> and the <see cref="BattleHistory"/> table only prove against a real database.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the reasons <c>testing.md</c> names: commencing saves twice inside a transaction, allocating its sequences is raw SQL, and the table's five check constraints
/// only mean anything when a database is enforcing them.
/// </para>
/// <para>
/// Games are built by the real <c>GameSetupService</c>, so the tiles, players and Turn Queue these rows point at are the ones play would give them. The behaviour that needs no database is
/// covered in <see cref="BattleLifecycleWriterTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class BattleLifecycleWriterPersistenceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;
    private const int _ROUND_NUMBER = 1;

    private int _gameId;
    private int _attackingPlayerId;
    private int _defendingPlayerId;
    private int _tileId;
    private int _playerTurnId;

    private BattleLifecycleWriter _writer = null!;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Battle Lifecycle Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            int[] playerIds = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();

            _attackingPlayerId = playerIds[0];
            _defendingPlayerId = playerIds[1];
            _tileId = await context.CampaignMaps.Where(t => t.GameId == _gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();
        }

        _playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        // The real allocator, so the rows carry the sequences play would give them.
        _writer = new BattleLifecycleWriter(NullLogger<BattleLifecycleWriter>.Instance, new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory));
    }

    private Battle NewBattle(params int[] playerIds)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new Battle
        {
            GameId = _gameId,
            CampaignMapTileId = _tileId,
            CommencedAtRoundNumber = _ROUND_NUMBER,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = [],
            BattleParticipants =
            [
                .. playerIds.Select(playerId => new BattleParticipant
                {
                    PlayerId = playerId,
                    JoinedAtRoundNumber = _ROUND_NUMBER,
                    IsStillInBattle = true,
                    ExitReason = BattleExitReason.NotApplicable,
                    RowVersion = []
                })
            ]
        };
    }

    private async Task<List<BattleHistory>> LoadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.BattleHistory.AsNoTracking().OrderBy(row => row.TurnSequence).ThenBy(row => row.Id).ToListAsync();
    }

    /// <summary>
    /// Commences a Battle through the writer, and hands back its new id.
    /// </summary>
    private async Task<int> CommenceBattleAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Battle battle = NewBattle(_attackingPlayerId, _defendingPlayerId);

        ServiceResultBase result = await _writer.CommenceAsync(context, battle, _playerTurnId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return battle.Id;
    }

    [Test, Description("Commencing saves the Battle and then its rows carrying the real id, on consecutive sequences from one allocation, so the whole start is one ordered run.")]
    public async Task ShouldRecordTheCommencementAndEveryFoundingSideOnConsecutiveSequencesAsync()
    {
        int battleId = await CommenceBattleAsync();

        List<BattleHistory> rows = await LoadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(battleId, Is.GreaterThan(0), "The rows can only carry the Battle's id because it is saved first.");
            Assert.That(rows.Select(row => row.EventType), Is.EqualTo(new[]
            {
                BattleHistoryEventType.Commenced,
                BattleHistoryEventType.ParticipantJoined,
                BattleHistoryEventType.ParticipantJoined
            }));
            Assert.That(rows.Select(row => row.ParticipantPlayerId), Is.EqualTo(new int?[] { null, _attackingPlayerId, _defendingPlayerId }));
            Assert.That(rows.Select(row => row.BattleId), Is.All.EqualTo(battleId));
            Assert.That(rows.Select(row => row.CampaignMapTileId), Is.All.EqualTo(_tileId));
            Assert.That(rows.Select(row => row.PlayerTurnId), Is.All.EqualTo(_playerTurnId));
            Assert.That(rows.Select(row => row.TurnSequence), Is.Ordered.And.Unique, "One allocation, so the run has no gaps another writer could slip into.");
            Assert.That(rows[2].TurnSequence - rows[0].TurnSequence, Is.EqualTo(2));
        }
    }

    [Test, Description("Commencing inside a caller's transaction joins it rather than committing on its own, so a Battle rolled back with the caller leaves no rows behind.")]
    public async Task ShouldJoinTheCallersTransactionAndRollBackWithItAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            ServiceResultBase result = await _writer.CommenceAsync(context, NewBattle(_attackingPlayerId, _defendingPlayerId), _playerTurnId);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

            await transaction.RollbackAsync();
        }

        await using AscendantEmpireContext reloadContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await LoadHistoryAsync(), Is.Empty);
            Assert.That(await reloadContext.Battles.AnyAsync(battle => battle.GameId == _gameId), Is.False);
        }
    }

    [Test, Description("A whole engagement's feed is one ordered run through the Turn, so the events interleave with the movement and structure feeds in the order they happened.")]
    public async Task ShouldRecordAWholeEngagementAsOneOrderedRunAsync()
    {
        int battleId = await CommenceBattleAsync();

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Battle battle = await context.Battles.Include(b => b.BattleParticipants).SingleAsync(b => b.Id == battleId);

            ServiceResultBase leaveResult = await _writer.LeaveAsync(context, battle, _defendingPlayerId, BattleExitReason.Routed, troopsLost: 120, _ROUND_NUMBER, _playerTurnId);

            Assert.That(leaveResult.HasSucceeded, Is.True, () => string.Join("; ", leaveResult.Errors));

            ServiceResultBase concludeResult = await _writer.ConcludeAsync(
                context,
                battle,
                new BattleConclusion
                {
                    RoundNumber = _ROUND_NUMBER,
                    VictorPlayerId = _attackingPlayerId,
                    GroundHeldByPlayerId = _attackingPlayerId,
                    Outcomes = [new BattleSideOutcome(_attackingPlayerId, BattleExitReason.Victory, TroopsLost: 20)]
                },
                _playerTurnId);

            Assert.That(concludeResult.HasSucceeded, Is.True, () => string.Join("; ", concludeResult.Errors));

            await context.SaveChangesAsync();
        }

        List<BattleHistory> rows = await LoadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.EventType), Is.EqualTo(new[]
            {
                BattleHistoryEventType.Commenced,
                BattleHistoryEventType.ParticipantJoined,
                BattleHistoryEventType.ParticipantJoined,
                BattleHistoryEventType.ParticipantLeft,
                BattleHistoryEventType.ParticipantLeft,
                BattleHistoryEventType.Concluded
            }));
            Assert.That(rows.Select(row => row.TurnSequence), Is.Ordered.And.Unique);
            Assert.That(rows[3].TroopsLost, Is.EqualTo(120), "The side that broke carries its own losses.");
            Assert.That(rows[4].TroopsLost, Is.EqualTo(20));
            Assert.That(rows[5].GroundHeldByPlayerId, Is.EqualTo(_attackingPlayerId));
        }
    }

    [Test, Description("Each check constraint rejects a malformed row outright, so the fold can read the nullable columns per event type without re-checking them.")]
    [TestCase(BattleHistoryEventType.Commenced, 5, null, null, null, Description = "A side named on an event that is not about one side.")]
    [TestCase(BattleHistoryEventType.ParticipantJoined, null, null, null, null, Description = "A join with no side named.")]
    [TestCase(BattleHistoryEventType.ParticipantLeft, 5, null, 10, null, Description = "A departure with no reason.")]
    [TestCase(BattleHistoryEventType.ParticipantLeft, 5, BattleExitReason.Routed, null, null, Description = "A departure with no losses.")]
    [TestCase(BattleHistoryEventType.ParticipantJoined, 5, BattleExitReason.Routed, null, null, Description = "A reason on an event that is not a departure.")]
    [TestCase(BattleHistoryEventType.ParticipantLeft, 5, BattleExitReason.Routed, -1, null, Description = "A side that lost a negative number of troops.")]
    [TestCase(BattleHistoryEventType.Commenced, null, null, null, 5, Description = "A ground-holder on an event that is not a conclusion.")]
    public async Task ShouldRefuseAMalformedRowAtTheDatabaseAsync(
        BattleHistoryEventType eventType,
        int? participantOffset,
        BattleExitReason? exitReason,
        int? troopsLost,
        int? groundHeldOffset)
    {
        int battleId = await CommenceBattleAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.BattleHistory.Add(new BattleHistory
        {
            GameId = _gameId,
            BattleId = battleId,
            EventType = eventType,
            // The offsets only say "a real Player" or "nobody"; which Player it is does not matter to the constraint.
            ParticipantPlayerId = participantOffset is null ? null : _attackingPlayerId,
            ExitReason = exitReason,
            TroopsLost = troopsLost,
            GroundHeldByPlayerId = groundHeldOffset is null ? null : _attackingPlayerId,
            CampaignMapTileId = _tileId,
            PlayerTurnId = _playerTurnId,
            TurnSequence = 500
        });

        Assert.That(async () => await context.SaveChangesAsync(), Throws.Exception);
    }

    [Test, Description("A conclusion is allowed to name nobody as holding the ground, since a battle can genuinely end with the field empty.")]
    public async Task ShouldAllowAConclusionWithNobodyHoldingTheGroundAsync()
    {
        int battleId = await CommenceBattleAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.BattleHistory.Add(new BattleHistory
        {
            GameId = _gameId,
            BattleId = battleId,
            EventType = BattleHistoryEventType.Concluded,
            ParticipantPlayerId = null,
            ExitReason = null,
            TroopsLost = null,
            GroundHeldByPlayerId = null,
            CampaignMapTileId = _tileId,
            PlayerTurnId = _playerTurnId,
            TurnSequence = 500
        });

        Assert.That(async () => await context.SaveChangesAsync(), Throws.Nothing);
    }
}
