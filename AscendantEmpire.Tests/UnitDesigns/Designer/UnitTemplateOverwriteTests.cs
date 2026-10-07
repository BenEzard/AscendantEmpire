using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Designer;

/// <summary>
/// Integration tests for saving a design over one that already holds its name: which clashes are refused outright, which the caller can confirm past with
/// <c>OverwriteExisting</c>, and what the confirmed overwrite leaves behind.
/// </summary>
/// <remarks>
/// Tier 3 (see testing.md): the save handlers wrap their work in <c>BeginTransactionAsync</c> and recompile the template's snapshot, and the assertions
/// read that snapshot's <c>Attack</c> complex type — none of which the in-memory provider can reproduce. The name-matching rules underneath are covered
/// more cheaply by <see cref="UnitTemplateNameLookupTests"/>.
/// </remarks>
internal class UnitTemplateOverwriteTests : SqlServerTestBase
{
    // The test body's two slot codes. Templates name the slot they fit into, so an empty slot is simply absent from the map.
    private const string SLOT_A = "slot-a";
    private const string SLOT_B = "slot-b";

    private ServiceProvider _provider = null!;

    private int _gameId;
    private int _playerId;

    /// <summary>
    /// Builds a real request pipeline over a scoped context, mirroring production: within one send, the save handler and the compile handler resolve the
    /// same scoped <see cref="AscendantEmpireContext"/>, so the save handler's transaction covers the compile too.
    /// </summary>
    [SetUp]
    public async Task BuildServiceProviderAsync()
    {
        // DbFactory is initialised by SqlServerTestBase before this SetUp runs; borrow its connection string.
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        string connectionString = context.Database.GetConnectionString()!;

        var services = new ServiceCollection();
        services.AddDbContext<AscendantEmpireContext>(o => o.UseSqlServer(connectionString));
        services.AddMediator(typeof(CreateUnitTemplateHandler).Assembly);
        services.AddLogging();

        // The save handlers stamp each snapshot with the game's current campaign round via ICampaignTurnService, so it has to be resolvable or the mediator
        // cannot activate them. CampaignTurnService opens its own contexts through a factory, so the base class's DbFactory is handed straight over.
        services.AddSingleton(DbFactory);
        services.AddScoped<ICampaignTurnService, CampaignTurnService>();

        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public void DisposeServiceProvider()
    {
        _provider?.Dispose();
    }

    /// <summary>
    /// Sends a request through a fresh DI scope, as production does per API request — every handler in the send chain shares one scoped context.
    /// </summary>
    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using IServiceScope scope = _provider.CreateScope();
        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }

    /// <summary>
    /// Seeds a two-slot body, a weapon and an armour, then a game whose single player has unlocked all three (the player-owned save path checks unlocks).
    /// </summary>
    private async Task SeedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var body = new UnitComponent
        {
            Code = "test_body",
            DisplayName = "Test Body",
            ComponentType = ComponentType.Body,
            Weight = 100,
            Body = new UnitComponentBody
            {
                MovementDomain = DomainType.Land,
                // Raw fixed-point (Scale 1,000): 3,000 = 3.0 campaign movement points.
                CampaignMovementPoints = 3000,
                BattlefieldMovementPoints = 6000,
                // Only bodies expose slots, so they live on the body extension. No rules, so anything may be fitted.
                Slots =
                [
                    new UnitComponentSlot { Code = SLOT_A, DisplayName = "Slot A", IsVisible = true, UIXPosition = 0, UIYPosition = 0 },
                    new UnitComponentSlot { Code = SLOT_B, DisplayName = "Slot B", IsVisible = true, UIXPosition = 1, UIYPosition = 0 },
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

        var armour = new UnitComponent
        {
            Code = "arm",
            DisplayName = "Test Armour",
            ComponentType = ComponentType.Armour,
            Weight = 30,
            Armour = new UnitComponentArmour { Resistance = new DamageProfile { Pierce = 250 } },
        };

        context.UnitComponents.AddRange(body, weapon, armour);
        await context.SaveChangesAsync();

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
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/ruler_01.png",
            Status = PlayerStatus.Active,
        };
        context.Players.Add(player);
        await context.SaveChangesAsync();

        List<int> componentIds = await context.UnitComponents.Select(c => c.Id).ToListAsync();
        context.PlayerUnlockedComponents.AddRange(componentIds.Select(componentId => new PlayerUnlockedComponent
        {
            PlayerId = player.Id,
            UnitComponentId = componentId,
            UnlockedOnTurn = 1,
        }));
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _playerId = player.Id;
    }

    /// <summary>
    /// Seeds a global/common template (no game, no player) under the given name, so the player-facing name rules can be tested against the shared catalogue.
    /// </summary>
    private async Task SeedGlobalTemplateAsync(string displayName)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int bodyId = await context.UnitComponents.Where(c => c.Code == "test_body").Select(c => c.Id).SingleAsync();

        context.UnitTemplates.Add(new UnitTemplate
        {
            Code = "seeded_global",
            DisplayName = displayName,
            MapImagePath = "global.png",
            BodyComponentId = bodyId,
        });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Fields one Unit built from a template, seeding the map tile it has to stand on.
    /// </summary>
    private async Task SeedUnitAsync(int templateId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // UnitInstance's tile foreign key is enforced here (unlike the in-memory tier), so a real CampaignMap row and its lookup rows are needed.
        context.CampaignBiomes.Add(new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MapEditorHtmlColor = "#90ee90",
            MovementCost = 1.0m,
            DoesBlockSight = false,
            IsWater = false,
            IsTraversable = true,
            RegenerationPercent = 0.1m,
            IsPaintableOnCampaignMap = true,
            CanBeElevated = true,
        });

        context.CampaignGeographies.Add(new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1.0m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true,
        });
        await context.SaveChangesAsync();

        GameIndex game = await context.GameIndex.SingleAsync(g => g.Id == _gameId);

        var tile = new CampaignMap
        {
            GameId = _gameId,
            XCoord = 0,
            YCoord = 0,
            BiomeTypeId = BiomeType.Grassland,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Biome = null!,
            Geography = null!,
            Game = game,
            FirstVisitor = null,
            LastVisitor = null,
            ControlledBy = null,
        };
        context.Add(tile);
        await context.SaveChangesAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            GameId = _gameId,
            PlayerId = _playerId,
            CampaignMapTileId = tile.CampaignMapId,
            UnitTemplateId = templateId,
            InitialStrength = 10,
            HealthyCount = 10,
        });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Loads a template's compiled snapshot (with its component children) through a fresh context, so only committed data is visible.
    /// </summary>
    private async Task<CompiledUnitTemplate> LoadSnapshotAsync(int templateId)
    {
        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        return await verify.CompiledUnitTemplates
            .Include(c => c.Components)
            .SingleAsync(c => c.UnitTemplateId == templateId);
    }

    private static SaveUnitTemplateDto Dto(string displayName, string fittedCode, bool overwriteExisting = false) => new()
    {
        DisplayName = displayName,
        MapImagePath = "design.png",
        BaseCode = "test_body",
        SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = fittedCode },
        OverwriteExisting = overwriteExisting,
    };

    private static SaveGlobalUnitTemplateDto GlobalDto(string code, string displayName, string fittedCode, bool overwriteExisting = false) => new()
    {
        Code = code,
        DisplayName = displayName,
        MapImagePath = "design.png",
        BaseCode = "test_body",
        SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = fittedCode },
        OverwriteExisting = overwriteExisting,
    };

    /// <summary>
    /// Reads the structured detail off a conflict result, failing the test when it is missing — the designer branches on this, so its absence is a bug
    /// rather than an acceptable shortfall.
    /// </summary>
    private static TemplateNameConflict ConflictDetail(ServiceResultBase result)
    {
        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict), () => string.Join("; ", result.Errors));
        Assert.That(result.ErrorDetail, Is.TypeOf<TemplateNameConflict>(), "A name clash must carry the detail the caller decides on.");

        return (TemplateNameConflict)result.ErrorDetail!;
    }

    /// <summary>
    /// Creates a design and asserts it saved, returning its id — the starting point for most of the tests below.
    /// </summary>
    private async Task<int> CreateDesignAsync(string displayName, string fittedCode)
    {
        ServiceResult<UnitTemplateView> created = await SendAsync(new CreateUnitTemplateRequest(_gameId, _playerId, Dto(displayName, fittedCode)));

        Assert.That(created.HasSucceeded, Is.True, () => string.Join("; ", created.Errors));

        return created.SuccessValue!.Id;
    }

    [Test, Description("An unconfirmed save onto a name the player already uses is refused, but reported as something confirming would resolve.")]
    public async Task UnconfirmedSaveOntoOwnDesignOffersAnOverwriteAsync()
    {
        await SeedAsync();
        await CreateDesignAsync("My Design", "wpn");

        // Different spelling of the same name: names are matched trimmed and case-insensitively.
        ServiceResult<UnitTemplateView> second = await SendAsync(new CreateUnitTemplateRequest(_gameId, _playerId, Dto("  my design ", "arm")));

        TemplateNameConflict conflict = ConflictDetail(second);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        int count = await verify.UnitTemplates.CountAsync(t => t.PlayerId == _playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conflict.CanOverwrite, Is.True, "Their own design, with no Units fielded — theirs to replace.");
            Assert.That(conflict.ExistingDisplayName, Is.EqualTo("My Design"), "Reported as it is actually spelled.");
            Assert.That(conflict.InstanceCount, Is.Zero);
            Assert.That(count, Is.EqualTo(1), "The refused save wrote nothing.");
        }
    }

    [Test, Description("Confirming the overwrite replaces the design in place and refreshes its compiled snapshot, without leaving a second design behind.")]
    public async Task ConfirmedSaveOverwritesInPlaceAsync()
    {
        await SeedAsync();
        int templateId = await CreateDesignAsync("My Design", "wpn");

        CompiledUnitTemplate before = await LoadSnapshotAsync(templateId);

        // The same save the designer re-sends once the player accepts the confirmation: same name, new loadout, flag set.
        ServiceResult<UnitTemplateView> overwritten =
            await SendAsync(new CreateUnitTemplateRequest(_gameId, _playerId, Dto("My Design", "arm", overwriteExisting: true)));

        Assert.That(overwritten.HasSucceeded, Is.True, () => string.Join("; ", overwritten.Errors));

        CompiledUnitTemplate after = await LoadSnapshotAsync(templateId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        int templateCount = await verify.UnitTemplates.CountAsync(t => t.PlayerId == _playerId);
        List<UnitTemplateComponent> fitted = await verify.UnitTemplateComponents.Where(c => c.UnitTemplateId == templateId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.TotalWeight, Is.EqualTo(110), "Baseline: body 100 + weapon 10.");
            Assert.That(before.Attack.Slash, Is.EqualTo(1000), "Baseline: from the fitted weapon.");

            Assert.That(overwritten.SuccessValue!.Id, Is.EqualTo(templateId), "The overwrite resolved to the design that held the name.");
            Assert.That(templateCount, Is.EqualTo(1), "Overwriting updates in place rather than adding a design.");
            Assert.That(fitted, Has.Count.EqualTo(1), "The previous fitted component was replaced, not accumulated.");
            Assert.That(after.TotalWeight, Is.EqualTo(130), "Body 100 + armour 30.");
            Assert.That(after.Attack.Slash, Is.Zero, "The weapon is gone, so the snapshot's attack went with it.");
        }
    }

    [Test, Description("A name belonging to a standard design is refused outright: the shared catalogue is not a player's to shadow, confirmed or not.")]
    public async Task SaveOntoAGlobalDesignNameIsNeverOverwritableAsync()
    {
        await SeedAsync();
        await SeedGlobalTemplateAsync("Legionary");

        ServiceResult<UnitTemplateView> unconfirmed = await SendAsync(new CreateUnitTemplateRequest(_gameId, _playerId, Dto("legionary", "wpn")));
        ServiceResult<UnitTemplateView> confirmed =
            await SendAsync(new CreateUnitTemplateRequest(_gameId, _playerId, Dto("legionary", "wpn", overwriteExisting: true)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ConflictDetail(unconfirmed).CanOverwrite, Is.False);
            Assert.That(unconfirmed.Errors.Single(), Does.Contain("standard design"));
            Assert.That(ConflictDetail(confirmed).CanOverwrite, Is.False, "The flag confirms an intent; it does not grant a permission.");
        }
    }

    [Test, Description("A design with Units in the field can't be redrawn under them, and the confirmation flag does not get past that.")]
    public async Task SaveOntoAFieldedDesignIsRefusedEvenWhenConfirmedAsync()
    {
        await SeedAsync();
        int templateId = await CreateDesignAsync("My Design", "wpn");
        await SeedUnitAsync(templateId);

        ServiceResult<UnitTemplateView> confirmed =
            await SendAsync(new CreateUnitTemplateRequest(_gameId, _playerId, Dto("My Design", "arm", overwriteExisting: true)));

        TemplateNameConflict conflict = ConflictDetail(confirmed);
        CompiledUnitTemplate snapshot = await LoadSnapshotAsync(templateId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conflict.CanOverwrite, Is.False, "Nothing the player confirms makes this one go through.");
            Assert.That(conflict.InstanceCount, Is.EqualTo(1));
            Assert.That(confirmed.Errors.Single(), Does.Contain("1 unit is"), "The count reads naturally at one Unit.");
            Assert.That(confirmed.Errors.Single(), Does.Contain("disband"), "The message says how to get past it.");
            Assert.That(snapshot.TotalWeight, Is.EqualTo(110), "The refused save left the design exactly as it was.");
        }
    }

    [Test, Description("The by-id update still refuses a rename onto another design, while keeping its own name is not a clash with itself.")]
    public async Task UpdateRejectsARenameOntoAnotherDesignAsync()
    {
        await SeedAsync();
        int firstId = await CreateDesignAsync("My Design", "wpn");
        await CreateDesignAsync("Other Design", "arm");

        ServiceResult<UnitTemplateView> renamed =
            await SendAsync(new UpdateUnitTemplateRequest(_gameId, _playerId, firstId, Dto("Other Design", "wpn")));

        // The same update keeping the design's own name must still succeed — the template is exempted from its own name check.
        ServiceResult<UnitTemplateView> unchanged =
            await SendAsync(new UpdateUnitTemplateRequest(_gameId, _playerId, firstId, Dto("My Design", "arm")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ConflictDetail(renamed).CanOverwrite, Is.False, "This endpoint addresses one design by id — a confirmation would resolve nothing.");
            Assert.That(unchanged.HasSucceeded, Is.True, () => string.Join("; ", unchanged.Errors));
        }
    }

    [Test, Description("The dev flow reports the Units built from a catalogue template, but lets the overwrite through once confirmed.")]
    public async Task GlobalSaveReportsUnitCountThenOverwritesOnConfirmationAsync()
    {
        await SeedAsync();

        ServiceResult<GlobalUnitTemplateView> created = await SendAsync(new CreateGlobalUnitTemplateRequest(GlobalDto("spearman", "Spearman", "wpn")));

        Assert.That(created.HasSucceeded, Is.True, () => string.Join("; ", created.Errors));

        await SeedUnitAsync(created.SuccessValue!.Id);

        ServiceResult<GlobalUnitTemplateView> unconfirmed = await SendAsync(new CreateGlobalUnitTemplateRequest(GlobalDto("spearman", "Spearman", "arm")));
        TemplateNameConflict conflict = ConflictDetail(unconfirmed);

        ServiceResult<GlobalUnitTemplateView> confirmed =
            await SendAsync(new CreateGlobalUnitTemplateRequest(GlobalDto("spearman", "Spearman", "arm", overwriteExisting: true)));

        Assert.That(confirmed.HasSucceeded, Is.True, () => string.Join("; ", confirmed.Errors));

        CompiledUnitTemplate after = await LoadSnapshotAsync(created.SuccessValue!.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conflict.CanOverwrite, Is.True, "Authoring the shared catalogue is what this flow is for.");
            Assert.That(conflict.InstanceCount, Is.EqualTo(1), "The dev is told what the change will affect.");
            Assert.That(confirmed.SuccessValue!.Id, Is.EqualTo(created.SuccessValue!.Id), "Overwrote in place rather than adding a template.");
            Assert.That(after.TotalWeight, Is.EqualTo(130), "Body 100 + armour 30 — the snapshot followed the overwrite.");
        }
    }
}
