using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Structures.Services;

/// <summary>
/// Tests for what <see cref="StructureLifecycleWriter"/> and the <see cref="StructureHistory"/> table only prove against a real database.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the reasons <c>testing.md</c> names: a founding during a Turn saves twice inside a transaction, allocating its sequence is raw SQL, and the table's check
/// constraint and the slot index razing frees only mean anything when a database is enforcing them.
/// </para>
/// <para>
/// Games are built by the real <c>GameSetupService</c>, so the tiles, players and Turn Queue these rows point at are the ones play would give them. The behaviour that needs no database is
/// covered in <see cref="StructureLifecycleWriterTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class StructureLifecycleWriterPersistenceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>A Player Turn id no row has, for provoking the foreign key on a history row.</summary>
    private const int _MISSING_PLAYER_TURN_ID = 999_999;

    /// <summary>
    /// How much work the fixture's watchtower takes. 100, so that a Structure's build points and the percentage the history rows snapshot read as the same number.
    /// </summary>
    private const long _WATCHTOWER_BUILD_POINTS = 100;

    private int _gameId;
    private int _ownerPlayerId;
    private int _captorPlayerId;
    private int _structureTypeId;
    private int _tileId;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Structure Lifecycle Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int[] playerIds = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();

        _ownerPlayerId = playerIds[0];
        _captorPlayerId = playerIds[1];
        _tileId = await context.CampaignMaps.Where(t => t.GameId == _gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();

        // The structure catalogue is authored data the app never creates, so the test stands one in.
        var watchtower = new Structure
        {
            Code = "watchtower",
            Name = "Watchtower",
            RequiresSlotType = TileSlotType.Primary,
            AllowedPositions = PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = _WATCHTOWER_BUILD_POINTS,
            SightRange = 2,
            SightDirection = DetectionDirection.FullRadius,
            MapImagePath = "watchtower.png",
            FullImagePath = "watchtower-full.png"
        };

        context.Set<Structure>().Add(watchtower);
        await context.SaveChangesAsync();

        _structureTypeId = watchtower.Id;
    }

    [Test, Description("A founding during a Turn saves the Structure and a Founded row carrying its real id and the Turn's first sequence.")]
    public async Task ShouldRecordAFoundingDuringATurnWithTheStructuresRealIdAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        StructureInstance structure = NewStructure(buildPointsProgress: 30);

        await FoundAsync(structure, playerTurnId);

        StructureInstance saved = await ReloadStructureAsync(structure.Id);
        StructureHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.EventType, Is.EqualTo(StructureHistoryEventType.Founded));
            Assert.That(row.StructureInstanceId, Is.EqualTo(structure.Id), "The row carries the id the Structure was given on insert.");
            Assert.That(row.CompletionPercent, Is.EqualTo(30));
            Assert.That(row.TurnSequence, Is.EqualTo(1));
            Assert.That(saved.CreatedOnTurnId, Is.EqualTo(playerTurnId));
            Assert.That(saved.CompletedOnTurnId, Is.Null);
        }
    }

    [Test, Description("A Structure founded already finished gets Founded then Completed on consecutive sequences, so completion always comes from a Completed row.")]
    public async Task ShouldRecordFoundedThenCompletedForAStructureFoundedFinishedAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        StructureInstance structure = NewStructure(buildPointsProgress: 100);

        await FoundAsync(structure, playerTurnId);

        List<StructureHistory> rows = await LoadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.EventType), Is.EqualTo(new[] { StructureHistoryEventType.Founded, StructureHistoryEventType.Completed }));
            Assert.That(rows.Select(row => row.TurnSequence), Is.EqualTo(new[] { 1, 2 }));
            Assert.That((await ReloadStructureAsync(structure.Id)).CompletedOnTurnId, Is.EqualTo(playerTurnId));
        }
    }

    [Test, Description("When the Founded row cannot be written, the Structure is rolled back with it, so nothing comes into existence unrecorded.")]
    public async Task ShouldFoundNothingWhenTheFoundedRowCannotBeWrittenAsync()
    {
        // An allocator that answers for a Turn that does not exist. The Structure's insert succeeds, and then the row's insert breaks the Player Turn foreign key.
        IPlayerTurnService lyingAllocator = Substitute.For<IPlayerTurnService>();

        lyingAllocator.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _MISSING_PLAYER_TURN_ID, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(1, 1)));

        var writer = new StructureLifecycleWriter(NullLogger<StructureLifecycleWriter>.Instance, lyingAllocator);

        ServiceResultBase result;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            result = await writer.FoundAsync(context, NewStructure(buildPointsProgress: 30), _MISSING_PLAYER_TURN_ID);
        }

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await verify.StructureInstances.AnyAsync(s => s.GameId == _gameId), Is.False, "The first save was rolled back along with the second.");
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("Founding, capture and razing in one Turn draw consecutive numbers from the one counter, in the order they happened.")]
    public async Task ShouldRecordALifeInTurnOrderAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        StructureInstance founded = NewStructure(buildPointsProgress: 30);

        await FoundAsync(founded, playerTurnId);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            StructureInstance structure = await context.StructureInstances.SingleAsync(s => s.Id == founded.Id);

            ServiceResultBase captureResult = await RealWriter().CaptureAsync(context, structure, _captorPlayerId, playerTurnId);
            ServiceResultBase razeResult = await RealWriter().RazeAsync(context, structure, playerTurnId);

            Assert.That(captureResult.HasSucceeded && razeResult.HasSucceeded, Is.True, () => string.Join("; ", captureResult.Errors.Concat(razeResult.Errors)));

            await context.SaveChangesAsync();
        }

        List<StructureHistory> rows = await LoadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.EventType), Is.EqualTo(new[] { StructureHistoryEventType.Founded, StructureHistoryEventType.Captured, StructureHistoryEventType.Razed }));
            Assert.That(rows.Select(row => row.TurnSequence), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(rows[1].PreviousOwnerPlayerId, Is.EqualTo(_ownerPlayerId));
            Assert.That(rows[2].PlayerId, Is.EqualTo(_captorPlayerId), "Razed as its captor's, since the capture came first.");
        }
    }

    [Test, Description("Razing frees the tile's slot for a new Structure while the razed one's row stays, which is what makes it a soft delete rather than a lost record.")]
    public async Task ShouldLetANewStructureTakeTheSlotOfARazedOneAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        StructureInstance first = NewStructure(buildPointsProgress: 100);

        await FoundAsync(first, playerTurnId);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            StructureInstance structure = await context.StructureInstances.SingleAsync(s => s.Id == first.Id);

            ServiceResultBase razeResult = await RealWriter().RazeAsync(context, structure, playerTurnId);

            Assert.That(razeResult.HasSucceeded, Is.True, () => string.Join("; ", razeResult.Errors));

            await context.SaveChangesAsync();
        }

        StructureInstance second = NewStructure(buildPointsProgress: 10);

        await FoundAsync(second, playerTurnId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await ReloadStructureAsync(first.Id)).RemovedOnTurnId, Is.EqualTo(playerTurnId));
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That((await ReloadStructureAsync(second.Id)).CampaignMapTileId, Is.EqualTo(_tileId), "The same slot of the same tile.");
        }
    }

    [Test, Description("A change once the Turn has closed is refused and stages nothing, since a row appended after the fold has run would be intelligence that never arrives.")]
    public async Task ShouldStageNothingOnceTheTurnHasClosedAsync()
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        StructureInstance founded = NewStructure(buildPointsProgress: 30);

        await FoundAsync(founded, playerTurnId);

        await using (AscendantEmpireContext closeContext = await DbFactory.CreateDbContextAsync())
        {
            int campaignQueueId = await closeContext.TurnQueues
                .Where(q => q.GameId == _gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null)
                .Select(q => q.Id)
                .SingleAsync();

            ServiceResultBase closeResult = await new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory).ClosePlayerTurnAsync(closeContext, campaignQueueId, _ownerPlayerId);

            Assert.That(closeResult.HasSucceeded, Is.True, () => string.Join("; ", closeResult.Errors));
        }

        ServiceResultBase result;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            StructureInstance structure = await context.StructureInstances.SingleAsync(s => s.Id == founded.Id);

            result = await RealWriter().RazeAsync(context, structure, playerTurnId);

            await context.SaveChangesAsync();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That((await ReloadStructureAsync(founded.Id)).RemovedOnTurnId, Is.Null);
            Assert.That((await LoadHistoryAsync()).Select(row => row.EventType), Is.EqualTo(new[] { StructureHistoryEventType.Founded }));
        }
    }

    [TestCase(StructureHistoryEventType.Captured, false, Description = "A Captured row that does not say who held the Structure before is refused, because the vision seam reads that owner from it.")]
    [TestCase(StructureHistoryEventType.Razed, true, Description = "A Razed row naming a previous owner is refused, since only a capture changes hands.")]
    public async Task ShouldRejectARowWhosePreviousOwnerDoesNotMatchItsEventTypeAsync(StructureHistoryEventType eventType, bool hasPreviousOwner)
    {
        int playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        StructureInstance founded = NewStructure(buildPointsProgress: 30);

        await FoundAsync(founded, playerTurnId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Written straight to the table rather than through the writer, which never builds a row like this. The constraint is what stops anything else that tries.
        context.StructureHistory.Add(new StructureHistory
        {
            GameId = _gameId,
            StructureInstanceId = founded.Id,
            StructureId = _structureTypeId,
            PlayerId = _captorPlayerId,
            PreviousOwnerPlayerId = hasPreviousOwner ? _ownerPlayerId : null,
            EventType = eventType,
            CompletionPercent = 30,
            CampaignMapTileId = _tileId,
            PlayerTurnId = playerTurnId,
            TurnSequence = 2
        });

        DbUpdateException? exception = Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.That(exception!.InnerException?.Message, Does.Contain("CK_StructureHistory_PreviousOwnerOnlyWhenCaptured"));
    }

    // ---------------------------------------------------------------------

    /// <summary>The writer over the real allocator, on the per-test database.</summary>
    private StructureLifecycleWriter RealWriter()
    {
        return new StructureLifecycleWriter(NullLogger<StructureLifecycleWriter>.Instance, new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory));
    }

    /// <summary>Founds a Structure through the real writer on a context of its own, failing the test if the founding is refused.</summary>
    private async Task FoundAsync(StructureInstance structure, int playerTurnId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await RealWriter().FoundAsync(context, structure, playerTurnId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    /// <summary>A new, unsaved watchtower belonging to the first player, in the first tile's primary slot.</summary>
    private StructureInstance NewStructure(long buildPointsProgress)
    {
        return new StructureInstance
        {
            GameId = _gameId,
            CampaignMapTileId = _tileId,
            StructureId = _structureTypeId,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = _ownerPlayerId,
            BuildPointsProgress = buildPointsProgress
        };
    }

    private async Task<StructureInstance> ReloadStructureAsync(int structureInstanceId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.StructureInstances.SingleAsync(s => s.Id == structureInstanceId);
    }

    private async Task<List<StructureHistory>> LoadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.StructureHistory.Where(h => h.GameId == _gameId).OrderBy(h => h.Id).ToListAsync();
    }
}
