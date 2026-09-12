using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Battles.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Enums.Battle;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Battles.Services;

/// <summary>
/// Tests for <see cref="BattleLifecycleWriter"/>: that every change it makes to an engagement comes with its history row, and that it makes no change it cannot record.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. Joining, leaving and concluding only stage changes on the caller's context, and a refusal happens before anything is staged, so none of these paths needs anything the
/// in-memory provider lacks. The sequence allocator is substituted, since allocation is raw SQL and is covered by the Player Turn fixtures.
/// </para>
/// <para>
/// Commencing opens a transaction, and the check constraints and the open-battle index need a real database to bite. Those are covered in <see cref="BattleLifecycleWriterPersistenceTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class BattleLifecycleWriterTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _ATTACKING_PLAYER_ID = 2;
    private const int _DEFENDING_PLAYER_ID = 3;
    private const int _RELIEVING_PLAYER_ID = 4;
    private const int _BATTLE_ID = 5;
    private const int _TILE_ID = 100;
    private const int _PLAYER_TURN_ID = 6;
    private const int _ROUND_NUMBER = 7;

    /// <summary>Deliberately not 1, so a row stamped with a default would not pass by coincidence.</summary>
    private const int _TURN_SEQUENCE = 9;

    private IPlayerTurnService _playerTurnService = null!;
    private BattleLifecycleWriter _writer = null!;

    [SetUp]
    public void SetUpWriter()
    {
        _playerTurnService = Substitute.For<IPlayerTurnService>();

        // The Turn is open by default and hands out however many numbers are asked for, starting at the same place.
        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(_TURN_SEQUENCE, _TURN_SEQUENCE + call.ArgAt<int>(2) - 1)));

        _writer = new BattleLifecycleWriter(NullLogger<BattleLifecycleWriter>.Instance, _playerTurnService);
    }

    private static Battle NewBattle(params int[] playerIds)
    {
        return new Battle
        {
            Id = _BATTLE_ID,
            GameId = _GAME_ID,
            CampaignMapTileId = _TILE_ID,
            CommencedAtRoundNumber = _ROUND_NUMBER,
            RowVersion = [],
            BattleParticipants =
            [
                .. playerIds.Select(playerId => new BattleParticipant
                {
                    BattleId = _BATTLE_ID,
                    PlayerId = playerId,
                    JoinedAtRoundNumber = _ROUND_NUMBER,
                    IsStillInBattle = true,
                    ExitReason = BattleExitReason.NotApplicable,
                    RowVersion = []
                })
            ]
        };
    }

    /// <summary>
    /// Seeds an open Battle between the attacker and the defender, and hands back a tracked copy of it on its own context.
    /// </summary>
    private async Task<(AscendantEmpireContext Context, Battle Battle)> SeedOpenBattleAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.Battles.Add(NewBattle(_ATTACKING_PLAYER_ID, _DEFENDING_PLAYER_ID));
            await seedContext.SaveChangesAsync();
        }

        AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        Battle battle = await context.Battles.Include(b => b.BattleParticipants).SingleAsync(b => b.Id == _BATTLE_ID);

        return (context, battle);
    }

    private async Task<List<BattleHistory>> LoadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.BattleHistory.AsNoTracking().OrderBy(row => row.TurnSequence).ToListAsync();
    }

    private static BattleConclusion ConclusionOf(int? victorPlayerId, int? groundHeldByPlayerId, params BattleSideOutcome[] outcomes)
    {
        return new BattleConclusion
        {
            RoundNumber = _ROUND_NUMBER,
            VictorPlayerId = victorPlayerId,
            GroundHeldByPlayerId = groundHeldByPlayerId,
            Outcomes = outcomes
        };
    }

    [Test, Description("An engagement nobody is in, and one whose roster names a side twice, are refused rather than recorded as a battle that cannot be folded sensibly.")]
    [TestCase(false, Description = "No sides at all.")]
    [TestCase(true, Description = "The same side twice.")]
    public async Task ShouldRefuseToCommenceAnEngagementWithAnUnusableRosterAsync(bool duplicateSide)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Battle battle = duplicateSide ? NewBattle(_ATTACKING_PLAYER_ID, _ATTACKING_PLAYER_ID) : NewBattle();
        battle.Id = 0;

        ServiceResultBase result = await _writer.CommenceAsync(context, battle, _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("A side joining an engagement under way is added to the roster with its own row, staged for the caller's save rather than saved here.")]
    public async Task ShouldStageAJoinAndItsRowForTheCallersSaveAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            ServiceResultBase result = await _writer.JoinAsync(context, battle, _RELIEVING_PLAYER_ID, _ROUND_NUMBER, _PLAYER_TURN_ID);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(await LoadHistoryAsync(), Is.Empty, "Nothing is saved until the caller saves.");

            await context.SaveChangesAsync();
        }

        BattleHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.EventType, Is.EqualTo(BattleHistoryEventType.ParticipantJoined));
            Assert.That(row.ParticipantPlayerId, Is.EqualTo(_RELIEVING_PLAYER_ID));
            Assert.That(row.TurnSequence, Is.EqualTo(_TURN_SEQUENCE));
            Assert.That(battle.BattleParticipants.Single(side => side.PlayerId == _RELIEVING_PLAYER_ID).IsStillInBattle, Is.True);
        }
    }

    [Test, Description("A side already on the roster cannot join again, since a second join row would tell watchers reinforcements arrived when nobody did.")]
    public async Task ShouldRefuseAJoinBySomebodyAlreadyInTheEngagementAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            ServiceResultBase result = await _writer.JoinAsync(context, battle, _DEFENDING_PLAYER_ID, _ROUND_NUMBER, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasSucceeded, Is.False);
                Assert.That(await LoadHistoryAsync(), Is.Empty);
            }
        }
    }

    [Test, Description("A side leaving is stamped out of the roster and its departure row carries how it went and what it cost, which is what a watcher is told about.")]
    public async Task ShouldStageADepartureAndItsRowForTheCallersSaveAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            ServiceResultBase result = await _writer.LeaveAsync(context, battle, _DEFENDING_PLAYER_ID, BattleExitReason.Routed, troopsLost: 40, _ROUND_NUMBER, _PLAYER_TURN_ID);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(await LoadHistoryAsync(), Is.Empty, "Nothing is saved until the caller saves.");

            await context.SaveChangesAsync();
        }

        BattleHistory row = (await LoadHistoryAsync()).Single();

        await using AscendantEmpireContext reloadContext = await DbFactory.CreateDbContextAsync();
        BattleParticipant participant = await reloadContext.BattleParticipants.AsNoTracking().SingleAsync(side => side.BattleId == _BATTLE_ID && side.PlayerId == _DEFENDING_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.EventType, Is.EqualTo(BattleHistoryEventType.ParticipantLeft));
            Assert.That(row.ParticipantPlayerId, Is.EqualTo(_DEFENDING_PLAYER_ID));
            Assert.That(row.ExitReason, Is.EqualTo(BattleExitReason.Routed));
            Assert.That(row.TroopsLost, Is.EqualTo(40));
            Assert.That(row.GroundHeldByPlayerId, Is.Null, "Who holds the ground is the conclusion's business, not a departure's.");
            Assert.That(participant.IsStillInBattle, Is.False);
            Assert.That(participant.LeftAtRoundNumber, Is.EqualTo(_ROUND_NUMBER));
            Assert.That(participant.ExitReason, Is.EqualTo(BattleExitReason.Routed));
        }
    }

    [Test, Description("A departure the feed could not describe honestly is refused: no reason at all, an undefined one, negative losses, or a side that is not there or already out.")]
    [TestCase(_DEFENDING_PLAYER_ID, BattleExitReason.NotApplicable, 40, Description = "NotApplicable is what a side still in the fight carries, so it cannot be why one left.")]
    [TestCase(_DEFENDING_PLAYER_ID, (BattleExitReason)99, 40, Description = "A reason that is not a BattleExitReason at all.")]
    [TestCase(_DEFENDING_PLAYER_ID, BattleExitReason.Retreat, -1, Description = "A side cannot lose a negative number of troops.")]
    [TestCase(_RELIEVING_PLAYER_ID, BattleExitReason.Retreat, 40, Description = "A side that was never in the engagement.")]
    public async Task ShouldRefuseADepartureItCouldNotDescribeAsync(int playerId, BattleExitReason exitReason, int troopsLost)
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            ServiceResultBase result = await _writer.LeaveAsync(context, battle, playerId, exitReason, troopsLost, _ROUND_NUMBER, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasSucceeded, Is.False);
                Assert.That(await LoadHistoryAsync(), Is.Empty);
            }
        }
    }

    [Test, Description("A side that has already left cannot leave again, since a second departure row would double its losses in every watcher's report.")]
    public async Task ShouldRefuseASecondDepartureByTheSameSideAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            await _writer.LeaveAsync(context, battle, _DEFENDING_PLAYER_ID, BattleExitReason.Retreat, troopsLost: 40, _ROUND_NUMBER, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            ServiceResultBase result = await _writer.LeaveAsync(context, battle, _DEFENDING_PLAYER_ID, BattleExitReason.Routed, troopsLost: 10, _ROUND_NUMBER, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasSucceeded, Is.False);
                Assert.That(await LoadHistoryAsync(), Has.Count.EqualTo(1), "Only the first departure was recorded.");
            }
        }
    }

    [Test, Description("Concluding writes out every side still in the fight and then the conclusion itself, so what each side lost and who held the ground are separate facts on separate sequences.")]
    public async Task ShouldRecordEverySideLeavingAndThenTheConclusionAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            BattleConclusion conclusion = ConclusionOf(
                victorPlayerId: _ATTACKING_PLAYER_ID,
                groundHeldByPlayerId: _ATTACKING_PLAYER_ID,
                new BattleSideOutcome(_ATTACKING_PLAYER_ID, BattleExitReason.Victory, TroopsLost: 20),
                new BattleSideOutcome(_DEFENDING_PLAYER_ID, BattleExitReason.Destroyed, TroopsLost: 120));

            ServiceResultBase result = await _writer.ConcludeAsync(context, battle, conclusion, _PLAYER_TURN_ID);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(await LoadHistoryAsync(), Is.Empty, "Nothing is saved until the caller saves.");

            await context.SaveChangesAsync();
        }

        List<BattleHistory> rows = await LoadHistoryAsync();

        await using AscendantEmpireContext reloadContext = await DbFactory.CreateDbContextAsync();
        Battle saved = await reloadContext.Battles.AsNoTracking().Include(b => b.BattleParticipants).SingleAsync(b => b.Id == _BATTLE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.EventType), Is.EqualTo(new[]
            {
                BattleHistoryEventType.ParticipantLeft,
                BattleHistoryEventType.ParticipantLeft,
                BattleHistoryEventType.Concluded
            }));
            Assert.That(rows.Select(row => row.TroopsLost), Is.EqualTo(new int?[] { 20, 120, null }));
            Assert.That(rows[2].GroundHeldByPlayerId, Is.EqualTo(_ATTACKING_PLAYER_ID));
            Assert.That(rows[2].ParticipantPlayerId, Is.Null);
            Assert.That(saved.ConcludedAtRoundNumber, Is.EqualTo(_ROUND_NUMBER));
            Assert.That(saved.VictorId, Is.EqualTo(_ATTACKING_PLAYER_ID));
            Assert.That(saved.BattleParticipants.Select(side => side.IsStillInBattle), Is.All.False);
        }
    }

    [Test, Description("A conclusion with nobody left holding the ground records that as nobody, since a draw that leaves the field empty is a real outcome rather than missing data.")]
    public async Task ShouldRecordAConclusionWithNobodyHoldingTheGroundAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            BattleConclusion conclusion = ConclusionOf(
                victorPlayerId: null,
                groundHeldByPlayerId: null,
                new BattleSideOutcome(_ATTACKING_PLAYER_ID, BattleExitReason.CeaseFire, TroopsLost: 20),
                new BattleSideOutcome(_DEFENDING_PLAYER_ID, BattleExitReason.CeaseFire, TroopsLost: 25));

            ServiceResultBase result = await _writer.ConcludeAsync(context, battle, conclusion, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        List<BattleHistory> rows = await LoadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows[2].EventType, Is.EqualTo(BattleHistoryEventType.Concluded));
            Assert.That(rows[2].GroundHeldByPlayerId, Is.Null);
        }
    }

    [Test, Description("A side that already left is written out once, by its own departure, so the conclusion's outcomes must cover exactly the sides still in the fight and no others.")]
    public async Task ShouldRefuseAConclusionWhoseOutcomesDoNotCoverTheSidesStillInTheEngagementAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            await _writer.LeaveAsync(context, battle, _DEFENDING_PLAYER_ID, BattleExitReason.Retreat, troopsLost: 40, _ROUND_NUMBER, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            BattleConclusion conclusion = ConclusionOf(
                victorPlayerId: _ATTACKING_PLAYER_ID,
                groundHeldByPlayerId: _ATTACKING_PLAYER_ID,
                new BattleSideOutcome(_ATTACKING_PLAYER_ID, BattleExitReason.Victory, TroopsLost: 20),
                new BattleSideOutcome(_DEFENDING_PLAYER_ID, BattleExitReason.Destroyed, TroopsLost: 120));

            ServiceResultBase result = await _writer.ConcludeAsync(context, battle, conclusion, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasSucceeded, Is.False);
                Assert.That(await LoadHistoryAsync(), Has.Count.EqualTo(1), "Only the earlier departure was recorded.");
            }
        }
    }

    [Test, Description("A victor who was never in the engagement is refused, since a conclusion naming an outsider would put a false ground-holder in every watcher's report.")]
    public async Task ShouldRefuseAConclusionWithAnOffRosterVictorAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            BattleConclusion conclusion = ConclusionOf(
                victorPlayerId: _RELIEVING_PLAYER_ID,
                groundHeldByPlayerId: _RELIEVING_PLAYER_ID,
                new BattleSideOutcome(_ATTACKING_PLAYER_ID, BattleExitReason.Victory, TroopsLost: 20),
                new BattleSideOutcome(_DEFENDING_PLAYER_ID, BattleExitReason.Destroyed, TroopsLost: 120));

            ServiceResultBase result = await _writer.ConcludeAsync(context, battle, conclusion, _PLAYER_TURN_ID);
            await context.SaveChangesAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasSucceeded, Is.False);
                Assert.That(await LoadHistoryAsync(), Is.Empty);
            }
        }
    }

    [Test, Description("An engagement that has already ended cannot be joined, left or concluded again: nothing more happens at it for a watcher to be told about.")]
    public async Task ShouldRefuseEveryChangeToAConcludedEngagementAsync()
    {
        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            battle.ConcludedAtRoundNumber = _ROUND_NUMBER;

            ServiceResultBase joinResult = await _writer.JoinAsync(context, battle, _RELIEVING_PLAYER_ID, _ROUND_NUMBER, _PLAYER_TURN_ID);
            ServiceResultBase leaveResult = await _writer.LeaveAsync(context, battle, _DEFENDING_PLAYER_ID, BattleExitReason.Retreat, troopsLost: 40, _ROUND_NUMBER, _PLAYER_TURN_ID);
            ServiceResultBase concludeResult = await _writer.ConcludeAsync(
                context,
                battle,
                ConclusionOf(victorPlayerId: null, groundHeldByPlayerId: null, new BattleSideOutcome(_ATTACKING_PLAYER_ID, BattleExitReason.CeaseFire, TroopsLost: 0)),
                _PLAYER_TURN_ID);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(joinResult.HasSucceeded, Is.False);
                Assert.That(leaveResult.HasSucceeded, Is.False);
                Assert.That(concludeResult.HasSucceeded, Is.False);
                Assert.That(await LoadHistoryAsync(), Is.Empty);
            }
        }
    }

    [Test, Description("No record, no change: a Turn whose sequence cannot be allocated leaves the engagement exactly as it was, on every method, rather than changing it unwitnessably.")]
    public async Task ShouldChangeNothingWhenTheSequenceCannotBeAllocatedAsync()
    {
        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Fail("The Turn has already been closed."));

        (AscendantEmpireContext context, Battle battle) = await SeedOpenBattleAsync();

        await using (context)
        {
            ServiceResultBase joinResult = await _writer.JoinAsync(context, battle, _RELIEVING_PLAYER_ID, _ROUND_NUMBER, _PLAYER_TURN_ID);
            ServiceResultBase leaveResult = await _writer.LeaveAsync(context, battle, _DEFENDING_PLAYER_ID, BattleExitReason.Retreat, troopsLost: 40, _ROUND_NUMBER, _PLAYER_TURN_ID);
            ServiceResultBase concludeResult = await _writer.ConcludeAsync(
                context,
                battle,
                ConclusionOf(
                    victorPlayerId: _ATTACKING_PLAYER_ID,
                    groundHeldByPlayerId: _ATTACKING_PLAYER_ID,
                    new BattleSideOutcome(_ATTACKING_PLAYER_ID, BattleExitReason.Victory, TroopsLost: 20),
                    new BattleSideOutcome(_DEFENDING_PLAYER_ID, BattleExitReason.Destroyed, TroopsLost: 120)),
                _PLAYER_TURN_ID);

            await context.SaveChangesAsync();

            await using AscendantEmpireContext reloadContext = await DbFactory.CreateDbContextAsync();
            Battle saved = await reloadContext.Battles.AsNoTracking().Include(b => b.BattleParticipants).SingleAsync(b => b.Id == _BATTLE_ID);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(joinResult.HasSucceeded, Is.False);
                Assert.That(leaveResult.HasSucceeded, Is.False);
                Assert.That(concludeResult.HasSucceeded, Is.False);
                Assert.That(await LoadHistoryAsync(), Is.Empty);
                Assert.That(saved.BattleParticipants, Has.Count.EqualTo(2), "Nobody was added to the roster.");
                Assert.That(saved.BattleParticipants.Select(side => side.IsStillInBattle), Is.All.True);
                Assert.That(saved.ConcludedAtRoundNumber, Is.Null);
                Assert.That(saved.VictorId, Is.Null);
            }
        }
    }
}
