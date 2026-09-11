using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Units.Services;

/// <summary>
/// Tests for <see cref="ForceLifecycleWriter"/>: that every change it makes to a Force comes with its history row, and that it makes no change it cannot record.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. A move and a destroy only stage changes on the caller's context, and a create with no Turn is one ordinary save, so none of these paths needs anything the in-memory
/// provider lacks. The sequence allocator is substituted, since allocation is raw SQL and is covered by the Player Turn fixtures.
/// </para>
/// <para>
/// A create <i>with</i> a Turn opens a transaction, which the in-memory provider cannot do, and the table's check constraint needs a real database to bite. Both are covered in
/// <see cref="ForceLifecycleWriterPersistenceTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class ForceLifecycleWriterTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _TEMPLATE_ID = 3;
    private const int _FORCE_ID = 4;
    private const int _START_TILE_ID = 100;
    private const int _DESTINATION_TILE_ID = 101;
    private const int _PLAYER_TURN_ID = 5;

    /// <summary>Deliberately not 1, so a row stamped with a default would not pass by coincidence.</summary>
    private const int _TURN_SEQUENCE = 9;

    private IPlayerTurnService _playerTurnService = null!;
    private ForceLifecycleWriter _writer = null!;

    [SetUp]
    public void SetUpWriter()
    {
        _playerTurnService = Substitute.For<IPlayerTurnService>();

        // The Turn is open by default and hands out one number.
        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(_TURN_SEQUENCE, _TURN_SEQUENCE)));

        _writer = new ForceLifecycleWriter(NullLogger<ForceLifecycleWriter>.Instance, _playerTurnService);
    }

    [Test, Description("A move stages the new tile and its Moved row without saving, so the caller's one save commits both or neither.")]
    public async Task ShouldStageTheNewTileAndItsMovedRowForTheCallersSaveAsync()
    {
        await SeedForceAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        UnitInstance force = await context.UnitInstances.SingleAsync(f => f.Id == _FORCE_ID);

        ServiceResultBase result = await _writer.MoveAsync(context, force, _DESTINATION_TILE_ID, _PLAYER_TURN_ID);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        Assert.That(await ReloadForceAsync(), Has.Property(nameof(UnitInstance.CampaignMapTileId)).EqualTo(_START_TILE_ID), "Nothing is saved until the caller saves.");

        await context.SaveChangesAsync();

        MovementHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await ReloadForceAsync()).CampaignMapTileId, Is.EqualTo(_DESTINATION_TILE_ID));
            Assert.That(row.EventType, Is.EqualTo(ForceHistoryEventType.Moved));
            Assert.That(row.UnitInstanceId, Is.EqualTo(_FORCE_ID));
            Assert.That(row.FromCampaignMapTileId, Is.EqualTo(_START_TILE_ID));
            Assert.That(row.CampaignMapTileId, Is.EqualTo(_DESTINATION_TILE_ID));
            Assert.That(row.PlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(row.TurnSequence, Is.EqualTo(_TURN_SEQUENCE));
            Assert.That(row.PlayerId, Is.EqualTo(_PLAYER_ID));
            Assert.That(row.UnitTemplateId, Is.EqualTo(_TEMPLATE_ID));
            Assert.That(row.GameId, Is.EqualTo(_GAME_ID));
        }
    }

    [Test, Description("A move whose sequence cannot be allocated stages nothing, so a caller that saves anyway still moves nothing.")]
    public async Task ShouldStageNothingWhenAMoveCannotBeRecordedAsync()
    {
        await SeedForceAsync();
        GivenTheTurnCannotAllocate();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        UnitInstance force = await context.UnitInstances.SingleAsync(f => f.Id == _FORCE_ID);

        ServiceResultBase result = await _writer.MoveAsync(context, force, _DESTINATION_TILE_ID, _PLAYER_TURN_ID);

        await context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That((await ReloadForceAsync()).CampaignMapTileId, Is.EqualTo(_START_TILE_ID));
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("A Force the context is not tracking is refused, because setting its tile would change an object the caller's save never looks at.")]
    public async Task ShouldRefuseToMoveAForceTheContextIsNotTrackingAsync()
    {
        await SeedForceAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        UnitInstance detachedForce = await context.UnitInstances.AsNoTracking().SingleAsync(f => f.Id == _FORCE_ID);

        ServiceResultBase result = await _writer.MoveAsync(context, detachedForce, _DESTINATION_TILE_ID, _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("Detached"));
            await _playerTurnService.DidNotReceive().AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A move onto the tile a Force already stands on is refused, since a row with matching tiles would give the fold a step with no direction.")]
    public async Task ShouldRefuseAMoveOntoTheTileTheForceAlreadyStandsOnAsync()
    {
        await SeedForceAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        UnitInstance force = await context.UnitInstances.SingleAsync(f => f.Id == _FORCE_ID);

        ServiceResultBase result = await _writer.MoveAsync(context, force, _START_TILE_ID, _PLAYER_TURN_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries<MovementHistory>(), Is.Empty);
        }
    }

    [Test, Description("Forces placed outside any Turn are saved with no history rows, since nobody could have watched them appear and there is no Turn to stamp.")]
    public async Task ShouldPlaceForcesWithoutRowsWhenThereIsNoTurnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance[] forces = [NewForce(), NewForce()];

        ServiceResultBase result = await _writer.CreateAsync(context, forces, playerTurnId: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(forces.Select(f => f.Id), Has.All.GreaterThan(0), "Create saves, so both Forces have been given their ids.");
            Assert.That(await LoadHistoryAsync(), Is.Empty);
            await _playerTurnService.DidNotReceive().AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A creation whose sequence cannot be allocated writes nothing, so no Force comes into existence unrecorded during a Turn.")]
    public async Task ShouldCreateNothingWhenACreationCannotBeRecordedAsync()
    {
        GivenTheTurnCannotAllocate();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _writer.CreateAsync(context, [NewForce()], _PLAYER_TURN_ID);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await verify.UnitInstances.AnyAsync(), Is.False);
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("A Force that already has an id exists in the database, so creating it again is refused before anything is written.")]
    public async Task ShouldRefuseToCreateAForceThatAlreadyHasAnIdAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance existingForce = NewForce();
        existingForce.Id = _FORCE_ID;

        ServiceResultBase result = await _writer.CreateAsync(context, [existingForce], playerTurnId: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(context.ChangeTracker.Entries(), Is.Empty);
        }
    }

    [Test, Description("A destroyed Force is deleted, and its Destroyed row keeps the Force's id so the fold can still follow a path that ended in death.")]
    public async Task ShouldRecordADestroyedRowAndDeleteTheForceAsync()
    {
        await SeedForceAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        UnitInstance force = await context.UnitInstances.SingleAsync(f => f.Id == _FORCE_ID);

        ServiceResultBase result = await _writer.DestroyAsync(context, force, _PLAYER_TURN_ID);

        await context.SaveChangesAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        MovementHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(await verify.UnitInstances.AnyAsync(f => f.Id == _FORCE_ID), Is.False, "Destruction is a hard delete.");
            Assert.That(row.EventType, Is.EqualTo(ForceHistoryEventType.Destroyed));
            Assert.That(row.UnitInstanceId, Is.EqualTo(_FORCE_ID));
            Assert.That(row.FromCampaignMapTileId, Is.Null);
            Assert.That(row.CampaignMapTileId, Is.EqualTo(_START_TILE_ID), "The Force died where it stood.");
            Assert.That(row.PlayerId, Is.EqualTo(_PLAYER_ID), "The owner is copied, so the row still says whose Force it was.");
            Assert.That(row.UnitTemplateId, Is.EqualTo(_TEMPLATE_ID));
            Assert.That(row.TurnSequence, Is.EqualTo(_TURN_SEQUENCE));
        }
    }

    [Test, Description("A destruction that cannot be recorded stages nothing, so the Force survives rather than vanishing from the intelligence record.")]
    public async Task ShouldKeepTheForceWhenItsDestructionCannotBeRecordedAsync()
    {
        await SeedForceAsync();
        GivenTheTurnCannotAllocate();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        UnitInstance force = await context.UnitInstances.SingleAsync(f => f.Id == _FORCE_ID);

        ServiceResultBase result = await _writer.DestroyAsync(context, force, _PLAYER_TURN_ID);

        await context.SaveChangesAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await verify.UnitInstances.AnyAsync(f => f.Id == _FORCE_ID), Is.True);
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    // ---------------------------------------------------------------------

    /// <summary>Makes the Turn refuse to allocate, as a Turn that has already been closed would.</summary>
    private void GivenTheTurnCannotAllocate()
    {
        _playerTurnService.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _PLAYER_TURN_ID, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Fail($"Player Turn Id {_PLAYER_TURN_ID} does not exist or has already ended."));
    }

    /// <summary>A new, unsaved Force on the start tile.</summary>
    private static UnitInstance NewForce()
    {
        return new UnitInstance
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            CampaignMapTileId = _START_TILE_ID,
            UnitTemplateId = _TEMPLATE_ID,
            InitialStrength = 100,
            HealthyCount = 100,
            InjuredCount = 0
        };
    }

    /// <summary>Saves the test Force on the start tile, as an existing row for the move and destroy tests to load.</summary>
    private async Task SeedForceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance force = NewForce();
        force.Id = _FORCE_ID;

        context.UnitInstances.Add(force);
        await context.SaveChangesAsync();
    }

    private async Task<UnitInstance> ReloadForceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UnitInstances.SingleAsync(f => f.Id == _FORCE_ID);
    }

    private async Task<List<MovementHistory>> LoadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.MovementHistory.OrderBy(row => row.Id).ToListAsync();
    }
}
