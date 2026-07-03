using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Integration tests for <see cref="UnlockSlotForPlayerHandler"/> against a real SQL Server database.
/// Covers the happy-path insert and the idempotent no-op when the slot is already unlocked.
/// </summary>
internal class UnlockSlotForPlayerHandlerTests : SqlServerTestBase
{
    private int _playerId;
    private int _slotId;

    /// <summary>Seeds a game + player and a body carrying a single slot, capturing the player and slot ids.</summary>
    private async Task SeedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            GameName = "Test Game",
            CampaignMapIndexId = 0,
            NumberOfPlayers = 1,
            CreationStatus = GameCreationStatus.SetupInProgress,
            PlayStatus = GamePlayStatus.SetupInProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 0,
            BattleMapSize = 50,
        };
        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        var player = new Player
        {
            GameId = game.Id,
            UserId = null,
            RulerTitle = "King",
            RulerName = "Tester",
            FactionName = "Testers",
            Status = PlayerStatus.Active,
        };
        context.Players.Add(player);
        await context.SaveChangesAsync();

        var body = new UnitComponent
        {
            Code = "test_body",
            DisplayName = "Test Body",
            ComponentType = ComponentType.Body,
            Weight = 100,
            Body = new UnitComponentBody { MovementDomain = DomainType.Land },
            Slots =
            [
                new UnitComponentSlot
                {
                    DisplayName = "Weapon Slot",
                    IsVisible = true,
                    IsLocked = true,
                    UIXPosition = 1,
                    UIYPosition = 0,
                    Rules =
                    [
                        new UnitComponentSlotRule
                        {
                            RuleType = SlotRuleType.ComponentType,
                            Operator = SlotRuleOperator.Equals,
                            Value = nameof(ComponentType.Weapon),
                        },
                    ],
                },
            ],
        };
        context.UnitComponents.Add(body);
        await context.SaveChangesAsync();

        _playerId = player.Id;
        _slotId = body.Slots.Single().Id;
    }

    [Test, Description("Unlocking a slot inserts a single PlayerUnlockedSlot row for the player.")]
    public async Task UnlockInsertsRowAsync()
    {
        await SeedAsync();

        var handler = new UnlockSlotForPlayerHandler(new NullLogger<UnlockSlotForPlayerHandler>(), DbFactory);
        ServiceResult<Empty> result =
            await handler.Handle(new UnlockSlotForPlayerCommand(_playerId, _slotId, UnlockedOnTurn: 3), CancellationToken.None);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        PlayerUnlockedSlot? row = await context.PlayerUnlockedSlots
            .SingleOrDefaultAsync(u => u.PlayerId == _playerId && u.UnitComponentSlotId == _slotId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(row, Is.Not.Null, "An unlock row should have been written.");
            Assert.That(row!.UnlockedOnTurn, Is.EqualTo(3));
        }
    }

    [Test, Description("Unlocking an already-unlocked slot succeeds without writing a duplicate row.")]
    public async Task UnlockIsIdempotentAsync()
    {
        await SeedAsync();

        var handler = new UnlockSlotForPlayerHandler(new NullLogger<UnlockSlotForPlayerHandler>(), DbFactory);
        var command = new UnlockSlotForPlayerCommand(_playerId, _slotId, UnlockedOnTurn: 3);

        // Two unlocks of the same slot for the same player.
        await handler.Handle(command, CancellationToken.None);
        ServiceResult<Empty> second = await handler.Handle(command, CancellationToken.None);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        int rowCount = await context.PlayerUnlockedSlots
            .CountAsync(u => u.PlayerId == _playerId && u.UnitComponentSlotId == _slotId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.HasSucceeded, Is.True);
            Assert.That(rowCount, Is.EqualTo(1), "A repeat unlock must not create a second row.");
        }
    }

    [Test, Description("A command missing player or slot fails validation without writing anything.")]
    public async Task UnlockRejectsInvalidRequestAsync()
    {
        await SeedAsync();

        var handler = new UnlockSlotForPlayerHandler(new NullLogger<UnlockSlotForPlayerHandler>(), DbFactory);
        ServiceResult<Empty> result =
            await handler.Handle(new UnlockSlotForPlayerCommand(PlayerId: 0, _slotId, UnlockedOnTurn: 1), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.HasSucceeded, Is.False);
        }
    }
}
