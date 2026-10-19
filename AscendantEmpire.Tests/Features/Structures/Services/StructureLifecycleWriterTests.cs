using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Structures;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Structures.Services;

/// <summary>
/// Tests for <see cref="StructureLifecycleWriter"/>: that every change it makes to a Structure comes with its history row, and that it makes no change it cannot record.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. Construction, capture and razing only stage changes on the caller's context, and a founding outside a Turn - or one refused before it starts - is at most one ordinary
/// save, so none of these paths needs anything the in-memory provider lacks. The sequence allocator is substituted, since allocation is raw SQL and is covered by the Player Turn fixtures.
/// </para>
/// <para>
/// A founding <i>during</i> a Turn opens a transaction, and the check constraint and the slot index need a real database to bite. Those are covered in
/// <see cref="StructureLifecycleWriterPersistenceTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class StructureLifecycleWriterTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OWNER_PLAYER_ID = 2;
    private const int _CAPTOR_PLAYER_ID = 3;
    private const int _STRUCTURE_TYPE_ID = 4;
    private const int _STRUCTURE_INSTANCE_ID = 5;
    private const int _TILE_ID = 100;
    private const int _PLAYER_TURN_ID = 6;

    /// <summary>How much work the fixture's kind takes. 100, so build points and the percentage the history rows snapshot read as the same number.</summary>
    private const long _STRUCTURE_BUILD_POINTS = 100;

    /// <summary>What the seeded Structure starts with: part built, so a step can advance it and a backwards step can be refused against it.</summary>
    private const long _SEEDED_BUILD_POINTS_PROGRESS = 40;

    /// <summary>Deliberately not 1, so a row stamped with a default would not pass by coincidence.</summary>
    private const int _TURN_SEQUENCE = 9;

    private IPlayerTurnService _playerTurnService = null!;
    private StructureLifecycleWriter _writer = null!;

    [SetUp]
    public async Task SetUpWriterAsync()
    {
        // The catalogue row the writer reads the kind's total from. Authored data the app never creates, so the test stands one in.
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.Structures.Add(new Structure
            {
                Id = _STRUCTURE_TYPE_ID,
                Code = "watchtower",
                Name = "Watchtower",
                RequiresSlotType = TileSlotType.Primary,
                AllowedPositions = PositionOnTile.WholeTile,
                CanBeBuiltOnCampaignMap = true,
                CanBeBuiltOnBattleMap = false,
                DualMapBuildTrigger = false,
                BuildPointsRequired = _STRUCTURE_BUILD_POINTS,
                SightRange = 2,
                SightDirection = DetectionDirection.FullRadius,
                MapImagePath = "watchtower.png",
                FullImagePath = "watchtower-full.png"
            });

            await context.SaveChangesAsync();
        }

        _playerTurnService = Substitute.For<IPlayerTurnService>();

        // The Turn is open by default and hands out one number.
        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(_TURN_SEQUENCE, _TURN_SEQUENCE)));

        _writer = new StructureLifecycleWriter(NullLogger<StructureLifecycleWriter>.Instance, _playerTurnService);
    }

    [Test, Description("Construction that advances without finishing stages the new progress and a Progressed row for the caller's save, and saves nothing itself.")]
    public async Task ShouldStageProgressAndItsRowForTheCallersSaveAsync()
    {
        await SeedStructureAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.ProgressAsync(context, structure, buildPointsProgress: 70, _PLAYER_TURN_ID);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        Assert.That((await ReloadStructureAsync()).BuildPointsProgress, Is.EqualTo(_SEEDED_BUILD_POINTS_PROGRESS), "Nothing is saved until the caller saves.");

        await context.SaveChangesAsync();

        StructureInstance saved = await ReloadStructureAsync();
        StructureHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.BuildPointsProgress, Is.EqualTo(70));
            Assert.That(saved.CompletedOnTurnId, Is.Null, "Still being built.");
            Assert.That(row.EventType, Is.EqualTo(StructureHistoryEventType.Progressed));
            Assert.That(row.CompletionPercent, Is.EqualTo(70), "The row snapshots the progress after the step.");
            Assert.That(row.StructureInstanceId, Is.EqualTo(_STRUCTURE_INSTANCE_ID));
            Assert.That(row.StructureId, Is.EqualTo(_STRUCTURE_TYPE_ID));
            Assert.That(row.PlayerId, Is.EqualTo(_OWNER_PLAYER_ID));
            Assert.That(row.PreviousOwnerPlayerId, Is.Null);
            Assert.That(row.CampaignMapTileId, Is.EqualTo(_TILE_ID));
            Assert.That(row.PlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(row.TurnSequence, Is.EqualTo(_TURN_SEQUENCE));
            Assert.That(row.GameId, Is.EqualTo(_GAME_ID));
        }
    }

    [Test, Description("A step past full is clamped to 100% and recorded as Completed with the Turn stamped, so a finished Structure has exactly one row saying so.")]
    public async Task ShouldRecordCompletionAndStampTheTurnWhenConstructionReachesFullAsync()
    {
        await SeedStructureAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.ProgressAsync(context, structure, buildPointsProgress: 150, _PLAYER_TURN_ID);

        await context.SaveChangesAsync();

        StructureInstance saved = await ReloadStructureAsync();
        StructureHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(saved.BuildPointsProgress, Is.EqualTo(_STRUCTURE_BUILD_POINTS), "A step past the total is clamped to it.");
            Assert.That(saved.CompletedOnTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(row.EventType, Is.EqualTo(StructureHistoryEventType.Completed));
            Assert.That(row.CompletionPercent, Is.EqualTo(100));
        }
    }

    [TestCase(_SEEDED_BUILD_POINTS_PROGRESS, Description = "Progress that stands still is refused, because a row for it would tell watchers construction moved when it did not.")]
    [TestCase(10L, Description = "Progress that goes backwards is refused, because construction is only ever undone by damage, which is not a construction step.")]
    public async Task ShouldRefuseProgressThatDoesNotAdvanceAsync(long buildPointsProgress)
    {
        await SeedStructureAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.ProgressAsync(context, structure, buildPointsProgress, _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries<StructureHistory>(), Is.Empty);
            await _playerTurnService.DidNotReceive().AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A Structure that is already finished cannot be built further, so its one Completed row is never followed by another.")]
    public async Task ShouldRefuseProgressOnAStructureThatIsAlreadyCompleteAsync()
    {
        await SeedStructureAsync(buildPointsProgress: _STRUCTURE_BUILD_POINTS);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.ProgressAsync(context, structure, buildPointsProgress: 100, _PLAYER_TURN_ID);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("A capture stages the new owner and a Captured row naming who held it before, which is what lets the old owner's view of it survive the Turn.")]
    public async Task ShouldStageACaptureWithThePreviousOwnerAsync()
    {
        await SeedStructureAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.CaptureAsync(context, structure, _CAPTOR_PLAYER_ID, _PLAYER_TURN_ID);

        await context.SaveChangesAsync();

        StructureHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That((await ReloadStructureAsync()).OwnedByPlayerId, Is.EqualTo(_CAPTOR_PLAYER_ID));
            Assert.That(row.EventType, Is.EqualTo(StructureHistoryEventType.Captured));
            Assert.That(row.PlayerId, Is.EqualTo(_CAPTOR_PLAYER_ID));
            Assert.That(row.PreviousOwnerPlayerId, Is.EqualTo(_OWNER_PLAYER_ID));
            Assert.That(row.CompletionPercent, Is.EqualTo(_SEEDED_BUILD_POINTS_PROGRESS), "A capture does not change how far built it is.");
        }
    }

    [Test, Description("A capture by the Player who already holds the Structure is refused, since a row whose old and new owners matched would report a change of hands that never happened.")]
    public async Task ShouldRefuseACaptureByTheCurrentOwnerAsync()
    {
        await SeedStructureAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.CaptureAsync(context, structure, _OWNER_PLAYER_ID, _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries<StructureHistory>(), Is.Empty);
        }
    }

    [Test, Description("Razing is a soft delete: the Structure's row stays with the Turn stamped, so every row pointing at it keeps pointing at something.")]
    public async Task ShouldStageARazingAsASoftDeleteAsync()
    {
        await SeedStructureAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.RazeAsync(context, structure, _PLAYER_TURN_ID);

        await context.SaveChangesAsync();

        StructureHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That((await ReloadStructureAsync()).RemovedOnTurnId, Is.EqualTo(_PLAYER_TURN_ID), "The row is still there to reload.");
            Assert.That(row.EventType, Is.EqualTo(StructureHistoryEventType.Razed));
            Assert.That(row.PlayerId, Is.EqualTo(_OWNER_PLAYER_ID), "Razed as the owner's Structure, whoever did the razing.");
        }
    }

    [Test, Description("A razed Structure can no longer be changed - not razed again, captured or built - so nothing is ever recorded happening to ruins.")]
    public async Task ShouldRefuseToChangeARazedStructureAsync()
    {
        await SeedStructureAsync(removedOnTurnId: 1);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase razeResult = await _writer.RazeAsync(context, structure, _PLAYER_TURN_ID);
        ServiceResultBase captureResult = await _writer.CaptureAsync(context, structure, _CAPTOR_PLAYER_ID, _PLAYER_TURN_ID);
        ServiceResultBase progressResult = await _writer.ProgressAsync(context, structure, buildPointsProgress: 90, _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(razeResult.HasSucceeded, Is.False);
            Assert.That(captureResult.HasSucceeded, Is.False);
            Assert.That(progressResult.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries<StructureHistory>(), Is.Empty);
        }
    }

    [Test, Description("A razing whose sequence cannot be allocated stages nothing, so the Structure stands rather than vanishing from the intelligence record.")]
    public async Task ShouldStageNothingWhenAChangeCannotBeRecordedAsync()
    {
        await SeedStructureAsync();
        GivenTheTurnCannotAllocate();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.RazeAsync(context, structure, _PLAYER_TURN_ID);

        await context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That((await ReloadStructureAsync()).RemovedOnTurnId, Is.Null);
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("A Structure the context is not tracking is refused, because changing it would change an object the caller's save never looks at.")]
    public async Task ShouldRefuseAStructureTheContextIsNotTrackingAsync()
    {
        await SeedStructureAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance detached = await context.StructureInstances.AsNoTracking().SingleAsync(s => s.Id == _STRUCTURE_INSTANCE_ID);

        ServiceResultBase result = await _writer.RazeAsync(context, detached, _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("Detached"));
            await _playerTurnService.DidNotReceive().AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A battle map Structure is refused, since nothing on a battle map is visible to a campaign map observer and its history would mislead the fold.")]
    public async Task ShouldRefuseABattleMapStructureAsync()
    {
        await SeedStructureAsync(battleMapTileId: 12);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        StructureInstance structure = await LoadTrackedAsync(context);

        ServiceResultBase result = await _writer.RazeAsync(context, structure, _PLAYER_TURN_ID);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [TestCase(30L, null, Description = "A Structure placed outside any Turn is saved with no rows and the no-Turn stamp, since nobody could have watched it appear.")]
    [TestCase(_STRUCTURE_BUILD_POINTS, 0, Description = "One placed outside any Turn already finished is stamped complete with the same no-Turn sentinel it was created with.")]
    public async Task ShouldFoundWithoutRowsWhenThereIsNoTurnAsync(long buildPointsProgress, int? expectedCompletedOnTurnId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        StructureInstance structure = NewStructure(buildPointsProgress);

        ServiceResultBase result = await _writer.FoundAsync(context, structure, playerTurnId: null);

        StructureInstance saved = await ReloadStructureAsync(structure.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(structure.Id, Is.GreaterThan(0), "Founding saves, so the Structure has been given its id.");
            Assert.That(saved.CreatedOnTurnId, Is.Zero);
            Assert.That(saved.CompletedOnTurnId, Is.EqualTo(expectedCompletedOnTurnId));
            Assert.That(await LoadHistoryAsync(), Is.Empty);
            await _playerTurnService.DidNotReceive().AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A Structure that already has an id exists in the database, so founding it again is refused before anything is written.")]
    public async Task ShouldRefuseToFoundAStructureThatAlreadyHasAnIdAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        StructureInstance existing = NewStructure(buildPointsProgress: 30);
        existing.Id = _STRUCTURE_INSTANCE_ID;

        ServiceResultBase result = await _writer.FoundAsync(context, existing, playerTurnId: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries(), Is.Empty);
        }
    }

    [TestCase(-1L, Description = "A Structure cannot be founded with negative progress, which no amount of building could have produced.")]
    [TestCase(_STRUCTURE_BUILD_POINTS + 1, Description = "A Structure cannot be founded with more work done than its kind takes, which would make every later comparison against the total lie.")]
    public async Task ShouldRefuseToFoundWithProgressOutsideTheRangeAsync(long buildPointsProgress)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _writer.FoundAsync(context, NewStructure(buildPointsProgress), _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries(), Is.Empty);
        }
    }

    [Test, Description("A founding whose sequence cannot be allocated writes nothing, so no Structure comes into existence unrecorded during a Turn.")]
    public async Task ShouldFoundNothingWhenTheFoundingCannotBeRecordedAsync()
    {
        GivenTheTurnCannotAllocate();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _writer.FoundAsync(context, NewStructure(buildPointsProgress: 30), _PLAYER_TURN_ID);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await verify.StructureInstances.AnyAsync(), Is.False);
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    // ---------------------------------------------------------------------

    /// <summary>Makes the Turn refuse to allocate, as a Turn that has already been closed would.</summary>
    private void GivenTheTurnCannotAllocate()
    {
        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Fail($"Player Turn Id {_PLAYER_TURN_ID} does not exist or has already ended."));
    }

    /// <summary>A new, unsaved campaign map Structure belonging to the owner.</summary>
    private static StructureInstance NewStructure(long buildPointsProgress, int? removedOnTurnId = null, int? battleMapTileId = null)
    {
        return new StructureInstance
        {
            GameId = _GAME_ID,
            CampaignMapTileId = _TILE_ID,
            BattleMapTileId = battleMapTileId,
            StructureId = _STRUCTURE_TYPE_ID,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = _OWNER_PLAYER_ID,
            CreatedOnTurnId = 1,
            BuildPointsProgress = buildPointsProgress,
            RemovedOnTurnId = removedOnTurnId
        };
    }

    /// <summary>Saves the test Structure, as an existing row for the change tests to load.</summary>
    private async Task SeedStructureAsync(long buildPointsProgress = _SEEDED_BUILD_POINTS_PROGRESS, int? removedOnTurnId = null, int? battleMapTileId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        StructureInstance structure = NewStructure(buildPointsProgress, removedOnTurnId, battleMapTileId);
        structure.Id = _STRUCTURE_INSTANCE_ID;

        context.StructureInstances.Add(structure);
        await context.SaveChangesAsync();
    }

    private static async Task<StructureInstance> LoadTrackedAsync(AscendantEmpireContext context)
    {
        return await context.StructureInstances.SingleAsync(s => s.Id == _STRUCTURE_INSTANCE_ID);
    }

    private async Task<StructureInstance> ReloadStructureAsync(int structureInstanceId = _STRUCTURE_INSTANCE_ID)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.StructureInstances.SingleAsync(s => s.Id == structureInstanceId);
    }

    private async Task<List<StructureHistory>> LoadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.StructureHistory.OrderBy(row => row.Id).ToListAsync();
    }
}
