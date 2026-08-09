using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Integration tests for the player-templates read endpoint: returns unlocked global templates plus the
/// player's own custom templates, and excludes global templates the player hasn't unlocked.
/// </summary>
internal class GetPlayerTemplatesHandlerTests : SqlServerTestBase
{
    private int _gameId;
    private int _playerId;

    /// <summary>
    /// Seeds a game + player, a body and a fitted weapon, and three templates: an unlocked global one,
    /// the player's own custom one, and a locked global one (no unlock row).
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

        var body = new UnitComponent
        {
            Code = "test_body",
            DisplayName = "Test Body",
            ComponentType = ComponentType.Body,
            Weight = 100,
            Body = new UnitComponentBody
            {
                MovementDomain = DomainType.Land,
                // Only bodies expose slots, so they live on the body extension.
                Slots =
                [
                    new UnitComponentSlot { Code = "WEAPON_1", DisplayName = "Weapon Slot", IsVisible = true, UIXPosition = 0, UIYPosition = 0 },
                ],
            },
        };
        var weapon = new UnitComponent
        {
            Code = "wpn",
            DisplayName = "Test Weapon",
            ComponentType = ComponentType.Weapon,
            Weight = 10,
            Weapon = new UnitComponentWeapon { Attack = new DamageProfile { Slash = 1000 }, MinimumRange = 1, MaximumRange = 2 },
        };
        context.UnitComponents.AddRange(body, weapon);
        await context.SaveChangesAsync();

        int slotId = body.Body!.Slots.Single().Id;

        // Unlocked global template (Code set, no game/player), prototyped.
        var globalTemplate = new UnitTemplate
        {
            Code = "tmpl_global",
            DisplayName = "Global Template",
            MapImagePath = "global.png",
            BodyComponentId = body.Id,
            Components = [new UnitTemplateComponent { UnitComponentSlotId = slotId, UnitComponentId = weapon.Id }],
        };

        // The player's own custom template (game + player set).
        var customTemplate = new UnitTemplate
        {
            Code = null,
            DisplayName = "My Custom Template",
            MapImagePath = "custom.png",
            GameId = game.Id,
            PlayerId = player.Id,
            BodyComponentId = body.Id,
            Components = [new UnitTemplateComponent { UnitComponentSlotId = slotId, UnitComponentId = weapon.Id }],
        };

        // Global template the player has NOT unlocked.
        var lockedTemplate = new UnitTemplate
        {
            Code = "tmpl_locked",
            DisplayName = "Locked Template",
            MapImagePath = "locked.png",
            BodyComponentId = body.Id,
        };

        context.UnitTemplates.AddRange(globalTemplate, customTemplate, lockedTemplate);
        await context.SaveChangesAsync();

        context.PlayerUnlockedTemplates.Add(new PlayerUnlockedTemplate
        {
            PlayerId = player.Id,
            UnitTemplateId = globalTemplate.Id,
            UnlockedOnTurn = 1,
            HasBeenPrototyped = true,
        });
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _playerId = player.Id;
    }

    [Test, Description("Returns unlocked global templates and the player's own custom templates, excluding locked globals.")]
    public async Task ReturnsUnlockedGlobalAndOwnCustomTemplatesAsync()
    {
        await SeedAsync();

        var handler = new GetPlayerTemplatesHandler(new NullLogger<GetPlayerTemplatesHandler>(), DbFactory);
        ServiceResult<PlayerTemplatesResult> result =
            await handler.Handle(new GetPlayerTemplatesRequest(_gameId, _playerId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
            Assert.That(result.SuccessValue!.Templates, Has.Count.EqualTo(2), "Unlocked global + own custom, not the locked global.");

            DesignTemplate global = result.SuccessValue!.Templates.Single(t => t.Code == "tmpl_global");
            Assert.That(global.IsCustom, Is.False);
            Assert.That(global.HasBeenPrototyped, Is.True, "The unlock row marks it prototyped.");
            Assert.That(global.BodyComponentCode, Is.EqualTo("test_body"));
            Assert.That(global.Components.Single().ComponentCode, Is.EqualTo("wpn"));

            DesignTemplate custom = result.SuccessValue!.Templates.Single(t => t.DisplayName == "My Custom Template");
            Assert.That(custom.IsCustom, Is.True);
            Assert.That(custom.Code, Is.Null);
            Assert.That(custom.HasBeenPrototyped, Is.False, "Custom template has no unlock row, so not prototyped.");

            Assert.That(result.SuccessValue!.Templates.Any(t => t.Code == "tmpl_locked"), Is.False, "Locked global is excluded.");
        }
    }
}
