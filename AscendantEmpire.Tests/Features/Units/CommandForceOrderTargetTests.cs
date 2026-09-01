using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Units;

/// <summary>
/// Pins the rule that a <see cref="ForceOrder"/> targets exactly one Force: either a <see cref="UnitInstance"/> or a
/// <see cref="Command"/>, never both and never neither.
/// </summary>
/// <remarks>
/// <para>
/// Runs at the SqlServer tier because the rule <b>is</b> a database check constraint. Both target columns are nullable so that either may be
/// absent, which means nothing in the type system can express "exactly one" — per <c>docs/testing.md</c>, a test that needs real constraint
/// enforcement cannot use the in-memory provider, which ignores constraints entirely and would pass while proving nothing.
/// </para>
/// <para>
/// The two rejection tests are the point of the fixture. Without them the constraint could be dropped from the model and every other test would
/// still pass, because nothing else ever writes a malformed order.
/// </para>
/// </remarks>
[TestFixture]
internal class CommandForceOrderTargetTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 3;
    private const int _MAP_HEIGHT = 3;

    /// <summary>The fragment SQL Server puts in the message when a CHECK constraint rejects a row.</summary>
    private const string _CHECK_CONSTRAINT_NAME = "CK_ForceOrders_UnitOrCommandXor";

    private async Task<int> SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Command Target Tests", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    /// <summary>
    /// Creates a Command owned by the game's first player, on the tile its first Unit already stands on.
    /// </summary>
    private static async Task<Command> AddCommandAsync(AscendantEmpireContext context, int gameId)
    {
        UnitInstance anchor = await FirstUnitAsync(context, gameId);

        Command command = new()
        {
            GameId = gameId,
            OwnerPlayerId = anchor.PlayerId,
            CampaignMapTileId = anchor.CampaignMapTileId,
            Name = "I Corps",
        };

        context.Commands.Add(command);
        await context.SaveChangesAsync();

        return command;
    }

    private static async Task<UnitInstance> FirstUnitAsync(AscendantEmpireContext context, int gameId)
    {
        return await context.UnitInstances.Where(u => u.GameId == gameId).OrderBy(u => u.Id).FirstAsync();
    }

    private static ForceOrder BuildOrder(int gameId, int? unitInstanceId, int? commandId)
    {
        return new ForceOrder
        {
            GameId = gameId,
            UnitInstanceId = unitInstanceId,
            CommandId = commandId,
            OrderType = ForceOrderType.SkipTurn,
            OrderStatus = ForceOrderStatus.Pending,
            CreatedTurnNumber = 1,
        };
    }

    [Test, Description("An order issued to a single Unit is the ordinary case and must save, because most Units never join a Command.")]
    public async Task ShouldSaveTheOrderWhenOnlyTheUnitIsTargetedAsync()
    {
        int gameId = await SetUpGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await FirstUnitAsync(context, gameId);

        context.ForceOrders.Add(BuildOrder(gameId, unitInstanceId: unit.Id, commandId: null));

        Assert.DoesNotThrowAsync(async () => await context.SaveChangesAsync());
    }

    [Test, Description("An order to a Command saves with no Unit named, because a Command moves as one rather than fanning out.")]
    public async Task ShouldSaveTheOrderWhenOnlyTheCommandIsTargetedAsync()
    {
        int gameId = await SetUpGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Command command = await AddCommandAsync(context, gameId);

        context.ForceOrders.Add(BuildOrder(gameId, unitInstanceId: null, commandId: command.Id));

        Assert.DoesNotThrowAsync(async () => await context.SaveChangesAsync());
    }

    [Test, Description("Naming both a Unit and a Command is ambiguous, so the database refuses it rather than letting one be picked.")]
    public async Task ShouldRejectTheOrderWhenBothTargetsAreSetAsync()
    {
        int gameId = await SetUpGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await FirstUnitAsync(context, gameId);
        Command command = await AddCommandAsync(context, gameId);

        context.ForceOrders.Add(BuildOrder(gameId, unitInstanceId: unit.Id, commandId: command.Id));

        DbUpdateException? exception = Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());

        Assert.That(exception!.GetBaseException().Message, Does.Contain(_CHECK_CONSTRAINT_NAME));
    }

    [Test, Description("Naming neither is attached to nothing, so the database refuses it rather than orphaning it in the queue.")]
    public async Task ShouldRejectTheOrderWhenNeitherTargetIsSetAsync()
    {
        int gameId = await SetUpGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.ForceOrders.Add(BuildOrder(gameId, unitInstanceId: null, commandId: null));

        DbUpdateException? exception = Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());

        Assert.That(exception!.GetBaseException().Message, Does.Contain(_CHECK_CONSTRAINT_NAME));
    }

    [Test, Description("A Unit reads back under its Command with its siblings, since vision and bonuses are defined by that membership.")]
    public async Task ShouldReadTheUnitBackUnderItsCommandWhenAttachedAsync()
    {
        int gameId = await SetUpGameAsync();

        int commandId;

        await using (AscendantEmpireContext writeContext = await DbFactory.CreateDbContextAsync())
        {
            Command command = await AddCommandAsync(writeContext, gameId);
            commandId = command.Id;

            UnitInstance unit = await FirstUnitAsync(writeContext, gameId);
            unit.CommandId = commandId;

            await writeContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext readContext = await DbFactory.CreateDbContextAsync();

        Command reloaded = await readContext.Commands.Include(c => c.Units).SingleAsync(c => c.Id == commandId);

        Assert.That(reloaded.Units, Has.Count.EqualTo(1));
        Assert.That(reloaded.Units.Single().CommandId, Is.EqualTo(commandId));
    }

    [Test, Description("A Unit with no Command is ordinary, so the membership round-trips as null rather than a coerced default.")]
    public async Task ShouldLeaveTheUnitUnattachedWhenNoCommandIsSetAsync()
    {
        int gameId = await SetUpGameAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance unit = await FirstUnitAsync(context, gameId);

        Assert.That(unit.CommandId, Is.Null);
    }
}
