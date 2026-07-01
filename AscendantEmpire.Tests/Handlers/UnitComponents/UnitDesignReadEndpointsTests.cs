using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Integration tests for the unit-design read endpoints (unlocked bodies + per-slot candidates),
/// against a real SQL Server database. Covers unlock scoping and server-side slot-rule filtering.
/// </summary>
internal class UnitDesignReadEndpointsTests : SqlServerTestBase
{
    private int _gameId;
    private int _playerId;

    // Component ids captured during seeding.
    private int _bodyId;
    private int _slotId;
    private int _unlockedWeaponId;

    /// <summary>
    /// Seeds a game + player, a catalog (one unlocked body with a Weapon-only slot, an unlocked weapon,
    /// a locked weapon, an unlocked armour, and a locked second body) and the player's unlock rows.
    /// </summary>
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

        var tag = new Tag { Code = "amphibious", DisplayName = "Amphibious", TagType = TagType.Ability };

        // Unlocked body with a single slot that accepts only Weapons.
        var body = new UnitComponent
        {
            Code = "test_body",
            DisplayName = "Test Body",
            ComponentType = ComponentType.Body,
            Weight = 100,
            Detection = 1000,
            Visibility = 2000,
            Tags = [new UnitComponentTag { Tag = tag }],
            Body = new UnitComponentBody
            {
                MovementDomain = DomainType.Land,
                CampaignMovementPoints = 3m,
                BattlefieldMovementPoints = 6m,
                MinimumUnitSize = 1,
                MaximumUnitSize = 10,
                SizeIncrement = 1,
            },
            // Slots now live on the component itself, not the body extension.
            Slots =
            [
                new UnitComponentSlot
                {
                    DisplayName = "Weapon Slot",
                    IsVisible = true,
                    IsLocked = false,
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

        UnitComponent unlockedWeapon = BuildWeapon("wpn_unlocked");
        UnitComponent lockedWeapon = BuildWeapon("wpn_locked");

        var unlockedArmour = new UnitComponent
        {
            Code = "arm_unlocked",
            DisplayName = "Unlocked Armour",
            ComponentType = ComponentType.Armour,
            Weight = 30,
            Armour = new UnitComponentArmour { Resistance = new DamageProfile { Pierce = 250 } },
        };

        // A second body the player has NOT unlocked.
        var lockedBody = new UnitComponent
        {
            Code = "body_locked",
            DisplayName = "Locked Body",
            ComponentType = ComponentType.Body,
            Weight = 100,
            Body = new UnitComponentBody { MovementDomain = DomainType.Water },
        };

        context.UnitComponents.AddRange(body, unlockedWeapon, lockedWeapon, unlockedArmour, lockedBody);
        await context.SaveChangesAsync();

        // Unlock everything except the locked weapon and the locked body.
        context.PlayerUnlockedComponents.AddRange(
            new PlayerUnlockedComponent { PlayerId = player.Id, UnitComponentId = body.Id, UnlockedOnTurn = 1 },
            new PlayerUnlockedComponent { PlayerId = player.Id, UnitComponentId = unlockedWeapon.Id, UnlockedOnTurn = 1 },
            new PlayerUnlockedComponent { PlayerId = player.Id, UnitComponentId = unlockedArmour.Id, UnlockedOnTurn = 1 });
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _playerId = player.Id;
        _bodyId = body.Id;
        _slotId = body.Slots.Single().Id;
        _unlockedWeaponId = unlockedWeapon.Id;
    }

    private static UnitComponent BuildWeapon(string code)
    {
        return new UnitComponent
        {
            Code = code,
            DisplayName = "Weapon " + code,
            ComponentType = ComponentType.Weapon,
            Weight = 10,
            Detection = 500,
            Visibility = 700,
            Weapon = new UnitComponentWeapon
            {
                Attack = new DamageProfile { Slash = 1000, Pierce = 500 },
                MinimumRange = 1,
                MaximumRange = 4,
                DomainModifiers = [new UnitComponentWeaponDomainModifier { Domain = DomainType.Land, DamageMultiplier = 1000 }],
            },
        };
    }

    [Test, Description("Bodies endpoint returns only unlocked bodies, with their slots, rules and tags.")]
    public async Task UnlockedBodiesReturnsOnlyUnlockedWithSlotsAsync()
    {
        await SeedAsync();

        var handler = new GetUnlockedBodiesHandler(new NullLogger<GetUnlockedBodiesHandler>(), DbFactory);
        ServiceResult<UnlockedBodiesResult> result =
            await handler.Handle(new GetUnlockedBodiesRequest(_gameId, _playerId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
            Assert.That(result.SuccessValue!.Bodies, Has.Count.EqualTo(1), "Only the unlocked body is returned.");

            DesignBody body = result.SuccessValue!.Bodies.Single();
            Assert.That(body.Code, Is.EqualTo("test_body"));
            Assert.That(body.MovementDomain, Is.EqualTo(DomainType.Land));
            Assert.That(body.Detection, Is.EqualTo(1000));
            Assert.That(body.TagCodes, Does.Contain("amphibious"));
            Assert.That(body.Slots, Has.Count.EqualTo(1));
            Assert.That(body.Slots.Single().Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Weapon)));
        }
    }

    [Test, Description("Slot-candidates returns only unlocked components that satisfy the slot's rules.")]
    public async Task SlotCandidatesReturnsOnlyUnlockedRulePassingComponentsAsync()
    {
        await SeedAsync();

        var handler = new GetSlotCandidateComponentsHandler(new NullLogger<GetSlotCandidateComponentsHandler>(), DbFactory);
        ServiceResult<SlotCandidateComponentsResult> result =
            await handler.Handle(new GetSlotCandidateComponentsRequest(_gameId, _playerId, _bodyId, _slotId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
            Assert.That(result.SuccessValue!.Components, Has.Count.EqualTo(1), "Only the unlocked weapon fits a Weapon-only slot.");

            DesignComponent candidate = result.SuccessValue!.Components.Single();
            Assert.That(candidate.Id, Is.EqualTo(_unlockedWeaponId));
            Assert.That(candidate.ComponentType, Is.EqualTo(ComponentType.Weapon));
            Assert.That(candidate.Weapon, Is.Not.Null, "Weapon stats should be populated.");
            Assert.That(candidate.Weapon!.Attack.Slash, Is.EqualTo(1000));
            Assert.That(candidate.Weapon.DomainModifiers.Single().Domain, Is.EqualTo(DomainType.Land));
            Assert.That(candidate.Armour, Is.Null);
        }
    }

    [Test, Description("Slot-candidates returns 404 when the slot does not belong to the named body.")]
    public async Task SlotCandidatesReturnsNotFoundForSlotBodyMismatchAsync()
    {
        await SeedAsync();

        var handler = new GetSlotCandidateComponentsHandler(new NullLogger<GetSlotCandidateComponentsHandler>(), DbFactory);
        ServiceResult<SlotCandidateComponentsResult> result =
            await handler.Handle(new GetSlotCandidateComponentsRequest(_gameId, _playerId, _bodyId, _slotId + 1000), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.HasSucceeded, Is.False);
        }
    }
}
