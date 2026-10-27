using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogueTransfer;
using AscendantEmpire.BackendContracts.UnitDesigns.RecompileAllUnitTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Compilation;

/// <summary>
/// Integration tests proving every template-writing path (player create/update, global create/update, catalogue bulk import)
/// compiles a <see cref="CompiledUnitTemplate"/> snapshot in the same save, and that a rejected save writes nothing at all.
/// </summary>
internal class SaveUnitTemplateCompilationTests : SqlServerTestBase
{
    // The test body's two slot codes. Templates name the slot they fit into, so an empty slot is simply
    // absent from the map rather than a null placeholder.
    private const string SLOT_A = "slot-a";
    private const string SLOT_B = "slot-b";

    private ServiceProvider _provider = null!;

    private int _gameId;
    private int _playerId;

    /// <summary>
    /// Builds a real request pipeline over a scoped context, mirroring production: within one send, the save handler and
    /// <see cref="CompileUnitTemplateHandler"/> resolve the same scoped <see cref="AscendantEmpireContext"/>, so the save
    /// handler's transaction covers the compile too.
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

        // The save handlers stamp each snapshot with the game's current campaign round via ICampaignTurnService, so it has to be resolvable
        // or the mediator cannot activate them. CampaignTurnService opens its own contexts through a factory rather than sharing the scoped one,
        // so the base class's DbFactory (already pointed at this test's database) is handed straight over.
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
    /// Seeds the shared component catalogue: a two-slot body (weight 100), a weapon (weight 10, slash 1,000) and an armour (weight 30, resist pierce 250).
    /// The slots carry no rules, so tests can fit and swap components freely.
    /// </summary>
    private async Task SeedCatalogueAsync()
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
                // Raw fixed-point (Scale 1,000): 3,000 = 3.0 campaign, 6,000 = 6.0 battlefield movement points.
                CampaignMovementPoints = 3000,
                BattlefieldMovementPoints = 6000,
                // Only bodies expose slots, so they live on the body extension.
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
    }

    /// <summary>
    /// Seeds a game and a player who has unlocked the whole catalogue (the player-owned save path checks unlocks; the global paths don't).
    /// </summary>
    private async Task SeedGameAndPlayerAsync()
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
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/01_western_king.png",
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
    /// Seeds an active (not completed) campaign turn queue for the seeded game, sitting on the given round.
    /// </summary>
    private async Task SeedCampaignQueueAsync(int currentRoundNumber)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TurnQueues.Add(new TurnQueue
        {
            GameId = _gameId,
            QueueType = TurnQueueType.Campaign,
            BattleId = null,
            CurrentRoundNumber = currentRoundNumber,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
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

    private static SaveUnitTemplateDto PlayerDto(string fittedCode) => new()
    {
        DisplayName = "My Design",
        MapImagePath = "design.png",
        BaseCode = "test_body",
        SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = fittedCode },
    };

    [Test, Description("Editing a catalogue component refreshes the snapshot of every template already built with it, with no explicit recompile.")]
    public async Task EditingComponentRecompilesDependentTemplateSnapshotsAsync()
    {
        await SeedCatalogueAsync();
        await SeedGameAndPlayerAsync();
        await SeedCampaignQueueAsync(currentRoundNumber: 7);

        ServiceResult<UnitTemplateView> created =
            await SendAsync(new CreateUnitTemplateRequest(GameId: _gameId, PlayerId: _playerId, PlayerDto("wpn")));

        Assert.That(created.HasSucceeded, Is.True, () => string.Join("; ", created.Errors));

        int templateId = created.SuccessValue!.Id;
        CompiledUnitTemplate before = await LoadSnapshotAsync(templateId);

        // Buff the fitted weapon through the component endpoint. Nothing here touches the template — the
        // snapshot would simply go stale without the recompile the component save now triggers.
        var buffedWeapon = new UpsertUnitComponentDto
        {
            Component = new UnitComponentDto
            {
                Code = "wpn",
                DisplayName = "Test Weapon",
                ComponentType = ComponentType.Weapon,
                Weight = 50,
                Weapon = new UnitComponentWeaponDto
                {
                    Attack = new DamageProfileDto { Slash = 5000 },
                    MinimumRange = 1,
                    MaximumRange = 2,
                },
            },
        };

        ServiceResult<UnitComponentView> updated = await SendAsync(new UpdateUnitComponentRequest("wpn", buffedWeapon));

        Assert.That(updated.HasSucceeded, Is.True, () => string.Join("; ", updated.Errors));

        CompiledUnitTemplate after = await LoadSnapshotAsync(templateId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.Attack.Slash, Is.EqualTo(1000), "Baseline: the snapshot held the original weapon's attack.");
            Assert.That(before.TotalWeight, Is.EqualTo(110), "Baseline: body 100 + weapon 10.");

            Assert.That(after.Attack.Slash, Is.EqualTo(5000), "The snapshot picks up the component's new attack.");
            Assert.That(after.TotalWeight, Is.EqualTo(150), "Body 100 + the weapon's new weight of 50.");
            Assert.That(after.CompiledOnTurn, Is.EqualTo(7), "Recompiled against the game's current campaign round.");
        }
    }

    [Test, Description("Creating a custom template compiles its snapshot in the same save, stamped with the game's current campaign round.")]
    public async Task CreateCompilesSnapshotStampedWithCurrentTurnAsync()
    {
        await SeedCatalogueAsync();
        await SeedGameAndPlayerAsync();
        await SeedCampaignQueueAsync(currentRoundNumber: 7);

        ServiceResult<UnitTemplateView> result =
            await SendAsync(new CreateUnitTemplateRequest(GameId: _gameId, PlayerId: _playerId, PlayerDto("wpn")));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        CompiledUnitTemplate snapshot = await LoadSnapshotAsync(result.SuccessValue!.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.CompiledOnTurn, Is.EqualTo(7), "Stamped with the active campaign queue's round.");
            Assert.That(snapshot.TotalWeight, Is.EqualTo(110), "Body 100 + weapon 10.");
            Assert.That(snapshot.Attack.Slash, Is.EqualTo(1000), "From the fitted weapon.");
            Assert.That(snapshot.Components, Has.Count.EqualTo(2), "The body plus the one fitted weapon.");
        }
    }

    [Test, Description("With no active campaign queue (e.g. the campaign hasn't started), the snapshot is stamped with the turn-0 sentinel.")]
    public async Task CreateStampsTurnZeroWithoutActiveCampaignQueueAsync()
    {
        await SeedCatalogueAsync();
        await SeedGameAndPlayerAsync();

        ServiceResult<UnitTemplateView> result =
            await SendAsync(new CreateUnitTemplateRequest(GameId: _gameId, PlayerId: _playerId, PlayerDto("wpn")));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        CompiledUnitTemplate snapshot = await LoadSnapshotAsync(result.SuccessValue!.Id);

        Assert.That(snapshot.CompiledOnTurn, Is.EqualTo(0), "No active campaign queue yields the 0 sentinel.");
    }

    [Test, Description("Updating a custom template recompiles its snapshot in place — new stats, still one row, children replaced.")]
    public async Task UpdateRecompilesSnapshotAsync()
    {
        await SeedCatalogueAsync();
        await SeedGameAndPlayerAsync();

        ServiceResult<UnitTemplateView> created =
            await SendAsync(new CreateUnitTemplateRequest(GameId: _gameId, PlayerId: _playerId, PlayerDto("wpn")));
        Assert.That(created.HasSucceeded, Is.True, () => string.Join("; ", created.Errors));

        int templateId = created.SuccessValue!.Id;

        // Swap the fitted weapon for the armour and save again.
        ServiceResult<UnitTemplateView> updated =
            await SendAsync(new UpdateUnitTemplateRequest(GameId: _gameId, PlayerId: _playerId, TemplateId: templateId, PlayerDto("arm")));
        Assert.That(updated.HasSucceeded, Is.True, () => string.Join("; ", updated.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        int snapshotCount = await verify.CompiledUnitTemplates.CountAsync(c => c.UnitTemplateId == templateId);
        CompiledUnitTemplate snapshot = await LoadSnapshotAsync(templateId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshotCount, Is.EqualTo(1), "Recompile upserts in place — still one snapshot row.");
            Assert.That(snapshot.Attack.Slash, Is.EqualTo(0), "The weapon is gone.");
            Assert.That(snapshot.Resistance.Pierce, Is.EqualTo(250), "From the newly fitted armour.");
            Assert.That(snapshot.TotalWeight, Is.EqualTo(130), "Body 100 + armour 30.");
            Assert.That(snapshot.Components.Count(c => c.UnitComponentSlotId != null), Is.EqualTo(1), "Children replaced, not appended.");
            Assert.That(snapshot.Components.Single(c => c.UnitComponentSlotId != null).Code, Is.EqualTo("arm"));
        }
    }

    [Test, Description("Global (dev catalogue) create and update both compile, stamped with turn 0 since global templates aren't game-scoped.")]
    public async Task GlobalCreateAndUpdateCompileAtTurnZeroAsync()
    {
        await SeedCatalogueAsync();

        var createDto = new SaveGlobalUnitTemplateDto
        {
            Code = "tmpl_global",
            DisplayName = "Global Template",
            MapImagePath = "global.png",
            BaseCode = "test_body",
            SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "wpn" },
        };

        ServiceResult<GlobalUnitTemplateView> created = await SendAsync(new CreateGlobalUnitTemplateRequest(createDto));
        Assert.That(created.HasSucceeded, Is.True, () => string.Join("; ", created.Errors));

        int templateId = created.SuccessValue!.Id;
        CompiledUnitTemplate afterCreate = await LoadSnapshotAsync(templateId);

        ServiceResult<GlobalUnitTemplateView> updated = await SendAsync(
            new UpdateGlobalUnitTemplateRequest(templateId, createDto with { SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "arm" } }));
        Assert.That(updated.HasSucceeded, Is.True, () => string.Join("; ", updated.Errors));

        CompiledUnitTemplate afterUpdate = await LoadSnapshotAsync(templateId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterCreate.CompiledOnTurn, Is.EqualTo(0), "Global templates are stamped with the turn-0 sentinel.");
            Assert.That(afterCreate.Attack.Slash, Is.EqualTo(1000), "From the fitted weapon.");
            Assert.That(afterUpdate.CompiledOnTurn, Is.EqualTo(0));
            Assert.That(afterUpdate.Resistance.Pierce, Is.EqualTo(250), "The update's recompile reflects the swapped-in armour.");
            Assert.That(afterUpdate.Attack.Slash, Is.EqualTo(0), "The weapon is gone.");
        }
    }

    [Test, Description("A rejected assembly writes nothing at all — no template row and no snapshot row.")]
    public async Task RejectedAssemblyWritesNothingAsync()
    {
        await SeedCatalogueAsync();

        ServiceResult<GlobalUnitTemplateView> result = await SendAsync(new CreateGlobalUnitTemplateRequest(new SaveGlobalUnitTemplateDto
        {
            Code = "tmpl_bad",
            DisplayName = "Bad Template",
            MapImagePath = "bad.png",
            BaseCode = "test_body",
            // A component code that doesn't exist in the catalogue fails resolution.
            SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "does_not_exist" },
        }));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        int templateCount = await verify.UnitTemplates.CountAsync();
        int snapshotCount = await verify.CompiledUnitTemplates.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(templateCount, Is.Zero, "The rejected template was not persisted.");
            Assert.That(snapshotCount, Is.Zero, "No orphaned snapshot either.");
        }
    }

    [Test, Description("Catalogue bulk import compiles a snapshot for every upserted template — both newly added and updated in place.")]
    public async Task BulkUpsertCompilesEverySnapshotAsync()
    {
        await SeedCatalogueAsync();

        var firstImport = new CatalogueTemplatesDocument
        {
            Templates =
            [
                new CatalogueTemplateDto { Code = "bulk_a", DisplayName = "Bulk A", MapImagePath = "a.png", BaseCode = "test_body", SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "wpn" } },
                new CatalogueTemplateDto { Code = "bulk_b", DisplayName = "Bulk B", MapImagePath = "b.png", BaseCode = "test_body", SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "arm" } },
            ],
        };

        ServiceResult<BulkUpsertGlobalUnitTemplatesResult> first = await SendAsync(new BulkUpsertGlobalUnitTemplatesRequest(firstImport));
        Assert.That(first.HasSucceeded, Is.True, () => string.Join("; ", first.Errors));

        await using AscendantEmpireContext lookup = await DbFactory.CreateDbContextAsync();
        int templateAId = (await lookup.UnitTemplates.SingleAsync(t => t.Code == "bulk_a")).Id;
        int templateBId = (await lookup.UnitTemplates.SingleAsync(t => t.Code == "bulk_b")).Id;

        CompiledUnitTemplate snapshotA = await LoadSnapshotAsync(templateAId);
        CompiledUnitTemplate snapshotB = await LoadSnapshotAsync(templateBId);

        // Re-import bulk_a with its weapon swapped for the armour: the existing snapshot must be recompiled.
        var secondImport = new CatalogueTemplatesDocument
        {
            Templates =
            [
                new CatalogueTemplateDto { Code = "bulk_a", DisplayName = "Bulk A", MapImagePath = "a.png", BaseCode = "test_body", SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "arm" } },
            ],
        };

        ServiceResult<BulkUpsertGlobalUnitTemplatesResult> second = await SendAsync(new BulkUpsertGlobalUnitTemplatesRequest(secondImport));
        Assert.That(second.HasSucceeded, Is.True, () => string.Join("; ", second.Errors));

        CompiledUnitTemplate snapshotAAfterReimport = await LoadSnapshotAsync(templateAId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshotA.Attack.Slash, Is.EqualTo(1000), "bulk_a was imported fitting the weapon.");
            Assert.That(snapshotB.Resistance.Pierce, Is.EqualTo(250), "bulk_b was imported fitting the armour.");
            Assert.That(snapshotAAfterReimport.Attack.Slash, Is.EqualTo(0), "The re-import removed bulk_a's weapon.");
            Assert.That(snapshotAAfterReimport.Resistance.Pierce, Is.EqualTo(250), "The re-import fitted bulk_a with the armour.");
        }
    }

    [Test, Description("Recompile-all refreshes every stale snapshot after a catalogue component edit, re-stamping each template's turn appropriately.")]
    public async Task RecompileAllRefreshesStaleSnapshotsAsync()
    {
        await SeedCatalogueAsync();
        await SeedGameAndPlayerAsync();
        await SeedCampaignQueueAsync(currentRoundNumber: 3);

        // One player-owned and one global template, both fitting the weapon (snapshot Attack.Slash 1,000 each).
        ServiceResult<UnitTemplateView> playerTemplate =
            await SendAsync(new CreateUnitTemplateRequest(GameId: _gameId, PlayerId: _playerId, PlayerDto("wpn")));
        Assert.That(playerTemplate.HasSucceeded, Is.True, () => string.Join("; ", playerTemplate.Errors));

        ServiceResult<GlobalUnitTemplateView> globalTemplate = await SendAsync(new CreateGlobalUnitTemplateRequest(new SaveGlobalUnitTemplateDto
        {
            Code = "tmpl_global",
            DisplayName = "Global Template",
            MapImagePath = "global.png",
            BaseCode = "test_body",
            SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "wpn" },
        }));
        Assert.That(globalTemplate.HasSucceeded, Is.True, () => string.Join("; ", globalTemplate.Errors));

        // Rebalance the weapon directly in the catalogue — save paths don't touch existing snapshots, so both go stale —
        // and advance the campaign to round 9 so the re-stamp is observable.
        await using (AscendantEmpireContext edit = await DbFactory.CreateDbContextAsync())
        {
            UnitComponentWeapon weapon = await edit.UnitComponents.Where(c => c.Code == "wpn").Select(c => c.Weapon!).SingleAsync();
            weapon.Attack.Slash = 2000;

            TurnQueue queue = await edit.TurnQueues.SingleAsync(q => q.GameId == _gameId);
            queue.CurrentRoundNumber = 9;

            await edit.SaveChangesAsync();
        }

        ServiceResult<RecompileAllUnitTemplatesResult> result = await SendAsync(new RecompileAllUnitTemplatesRequest());
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        CompiledUnitTemplate playerSnapshot = await LoadSnapshotAsync(playerTemplate.SuccessValue!.Id);
        CompiledUnitTemplate globalSnapshot = await LoadSnapshotAsync(globalTemplate.SuccessValue!.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.RecompiledCount, Is.EqualTo(2), "Both templates were recompiled.");
            Assert.That(result.SuccessValue!.Failures, Is.Empty);
            Assert.That(playerSnapshot.Attack.Slash, Is.EqualTo(2000), "The rebalanced weapon propagated to the player template's snapshot.");
            Assert.That(globalSnapshot.Attack.Slash, Is.EqualTo(2000), "And to the global template's snapshot.");
            Assert.That(playerSnapshot.CompiledOnTurn, Is.EqualTo(9), "Player-owned templates re-stamp with their game's current round.");
            Assert.That(globalSnapshot.CompiledOnTurn, Is.EqualTo(0), "Global templates keep the turn-0 sentinel.");
        }
    }

    /// <summary>
    /// Creates a MilitaryOrganisationalGroup tag (if it doesn't exist yet) and attaches it to the catalogue
    /// component with the given code. Returns the tag's id.
    /// </summary>
    private async Task<int> AttachOrganisationTagAsync(string componentCode, string tagCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Tag? tag = await context.Tags.SingleOrDefaultAsync(t => t.Code == tagCode);

        if (tag is null)
        {
            tag = new Tag
            {
                Code = tagCode,
                DisplayName = tagCode,
                TagType = TagType.MilitaryOrganisationalGroup,
            };
            context.Tags.Add(tag);
        }

        UnitComponent component = await context.UnitComponents.SingleAsync(c => c.Code == componentCode);
        context.UnitComponentTags.Add(new UnitComponentTag { UnitComponent = component, Tag = tag });
        await context.SaveChangesAsync();

        return tag.Id;
    }

    [Test, Description("A fitted component's organisation tag overrides the body's default on the compiled snapshot.")]
    public async Task CompileResolvesFittedOrganisationTagOverBodyDefaultAsync()
    {
        await SeedCatalogueAsync();
        await AttachOrganisationTagAsync("test_body", "infantry");
        int fittedTagId = await AttachOrganisationTagAsync("wpn", OrganisationTagCodes.SETTLER);

        ServiceResult<GlobalUnitTemplateView> result = await SendAsync(new CreateGlobalUnitTemplateRequest(new SaveGlobalUnitTemplateDto
        {
            Code = "tmpl_org",
            DisplayName = "Org Template",
            MapImagePath = "org.png",
            BaseCode = "test_body",
            SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "wpn" },
        }));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        CompiledUnitTemplate snapshot = await LoadSnapshotAsync(result.SuccessValue!.Id);
        Assert.That(snapshot.OrganisationTagId, Is.EqualTo(fittedTagId), "camping-goods-style override: the fitted component's tag wins.");
    }

    [Test, Description("With no fitted organisation tag, the body's default organisation tag lands on the snapshot.")]
    public async Task CompileFallsBackToBodyOrganisationTagAsync()
    {
        await SeedCatalogueAsync();
        int bodyTagId = await AttachOrganisationTagAsync("test_body", "infantry");

        ServiceResult<GlobalUnitTemplateView> result = await SendAsync(new CreateGlobalUnitTemplateRequest(new SaveGlobalUnitTemplateDto
        {
            Code = "tmpl_body_org",
            DisplayName = "Body Org Template",
            MapImagePath = "org.png",
            BaseCode = "test_body",
            SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "wpn" },
        }));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        CompiledUnitTemplate snapshot = await LoadSnapshotAsync(result.SuccessValue!.Id);
        Assert.That(snapshot.OrganisationTagId, Is.EqualTo(bodyTagId));
    }

    [Test, Description("With no organisation tags anywhere, the snapshot's organisation tag is null.")]
    public async Task CompileLeavesOrganisationTagNullWhenNoneCarriedAsync()
    {
        await SeedCatalogueAsync();

        ServiceResult<GlobalUnitTemplateView> result = await SendAsync(new CreateGlobalUnitTemplateRequest(new SaveGlobalUnitTemplateDto
        {
            Code = "tmpl_no_org",
            DisplayName = "No Org Template",
            MapImagePath = "org.png",
            BaseCode = "test_body",
            SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "wpn" },
        }));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        CompiledUnitTemplate snapshot = await LoadSnapshotAsync(result.SuccessValue!.Id);
        Assert.That(snapshot.OrganisationTagId, Is.Null);
    }

    [Test, Description("Two different organisation tags across the fitted components make the organisation ambiguous — the save fails and writes nothing.")]
    public async Task CompileRejectsAmbiguousFittedOrganisationTagsAsync()
    {
        await SeedCatalogueAsync();
        await AttachOrganisationTagAsync("wpn", OrganisationTagCodes.SETTLER);
        await AttachOrganisationTagAsync("arm", "scout");

        ServiceResult<GlobalUnitTemplateView> result = await SendAsync(new CreateGlobalUnitTemplateRequest(new SaveGlobalUnitTemplateDto
        {
            Code = "tmpl_ambiguous",
            DisplayName = "Ambiguous Template",
            MapImagePath = "org.png",
            BaseCode = "test_body",
            SlotComponentCodes = new Dictionary<string, string> { [SLOT_A] = "wpn", [SLOT_B] = "arm" },
        }));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        int templateCount = await verify.UnitTemplates.CountAsync();
        int snapshotCount = await verify.CompiledUnitTemplates.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("more than one military organisation tag"));
            Assert.That(templateCount, Is.Zero, "The rejected template was not persisted.");
            Assert.That(snapshotCount, Is.Zero, "No orphaned snapshot either.");
        }
    }
}
