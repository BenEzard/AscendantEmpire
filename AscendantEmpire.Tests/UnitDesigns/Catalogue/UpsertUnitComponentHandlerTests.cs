using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetComponent;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.UnitDesigns;
using AscendantEmpire.Tests.UnitDesigns.Shared;

using static AscendantEmpire.Tests.UnitDesigns.Shared.UnitDesignPayloads;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Exercises the single-component create/update endpoints — the granular write path the Entity Setup Tool
/// uses instead of the catalogue bulk importer.
/// </summary>
/// <remarks>
/// Runs against a real SQL Server for the same reason the bulk tests do — these tests read back the
/// <c>DamageProfile</c> complex type, which the in-memory provider throws on (see <c>docs/testing.md</c>) —
/// and because the fitted-component foreign key the update tests turn on is only enforced by a real
/// relational engine. The 1:1 shared-PK extensions are not a factor; those work in memory.
/// </remarks>
internal class UpsertUnitComponentHandlerTests : SqlServerTestBase
{
    // Aliased rather than referenced through UnitDesignPayloads at every call site, which would add noise
    // without adding meaning — the codes only matter here as "the body" and "the weapon".
    private const string BODY_CODE = UnitDesignPayloads.BODY_CODE;
    private const string WEAPON_CODE = UnitDesignPayloads.WEAPON_CODE;

    private async Task<CreateUnitComponentHandler> CreateHandlerAsync()
    {
        AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        return new CreateUnitComponentHandler(new NullLogger<CreateUnitComponentHandler>(), context);
    }

    private async Task<UpdateUnitComponentHandler> UpdateHandlerAsync()
    {
        AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // These tests are about the component graph, not the snapshots downstream of it — SaveUnitTemplateCompilationTests
        // covers those for real. The stub still has to answer the compile request rather than being left bare, though:
        // AffectedTemplateRecompiler sends one CompileUnitTemplateRequest per template built on an edited component and
        // reads the result, so an unconfigured substitute fails every test that seeds a fitted template on the recompile
        // instead of on what it set out to assert.
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CompileUnitTemplateRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<int>.Succeed(0));

        return new UpdateUnitComponentHandler(
            new NullLogger<UpdateUnitComponentHandler>(),
            context,
            mediator,
            Substitute.For<ICampaignTurnService>());
    }

    /// <summary>
    /// Wraps a component DTO in the endpoint payload, with no supersession links unless given.
    /// </summary>
    private static UpsertUnitComponentDto Payload(UnitComponentDto component, params ComponentSupersessionLinkDto[] supersededBy)
    {
        return new UpsertUnitComponentDto { Component = component, SupersededBy = supersededBy };
    }

    /// <summary>
    /// Creates the given component through the create endpoint, asserting it succeeded.
    /// </summary>
    private async Task CreateAsync(UnitComponentDto component, params ComponentSupersessionLinkDto[] supersededBy)
    {
        CreateUnitComponentHandler handler = await CreateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new CreateUnitComponentRequest(Payload(component, supersededBy)), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("Creating a component persists its graph and reports the new row, returning 201.")]
    public async Task CreatePersistsComponentAsync()
    {
        CreateUnitComponentHandler handler = await CreateHandlerAsync();

        ServiceResult<UnitComponentView> result = await handler.Handle(
            new CreateUnitComponentRequest(Payload(BuildBodyWithSlots(BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon))))),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent persisted = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(body => body.Slots).ThenInclude(slot => slot.Rules)
            .SingleAsync(component => component.Code == BODY_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created), "A new component reports Created.");
            Assert.That(result.SuccessValue!.Id, Is.EqualTo(persisted.Id));
            Assert.That(result.SuccessValue.SlotIds, Is.EqualTo([persisted.Body!.Slots.Single().Id]), "The saved slot ids come back for the client to reuse.");
            Assert.That(persisted.Body.Slots.Single().DisplayName, Is.EqualTo("Weapon 1"));
            Assert.That(persisted.Body.Slots.Single().Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Weapon)));
        }
    }

    [Test, Description("Creating a component whose code already exists is a conflict, not a silent update.")]
    public async Task CreateWithExistingCodeConflictsAsync()
    {
        await CreateAsync(BuildWeapon(WEAPON_CODE));

        CreateUnitComponentHandler handler = await CreateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new CreateUnitComponentRequest(Payload(BuildWeapon(WEAPON_CODE) with { DisplayName = "Duplicate" })),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        string persistedName = await verifyContext.UnitComponents
            .Where(component => component.Code == WEAPON_CODE)
            .Select(component => component.DisplayName)
            .SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(persistedName, Is.EqualTo("Test Weapon"), "The existing component is left alone.");
        }
    }

    [Test, Description("Adding a slot through the update endpoint keeps existing slot rows and the template fittings that reference them.")]
    public async Task UpdateAddingSlotPreservesFittingsAsync()
    {
        // The originally reported failure, now exercised through the granular endpoint the dev tool uses.
        await CreateAsync(BuildBodyWithSlots(BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon))));
        await CreateAsync(BuildWeapon(WEAPON_CODE));

        // The body has exactly one slot at this point, so the fitted one is the only one.
        int originalSlotId = (await DbFactory.FitWeaponIntoFirstSlotAsync("Fitted Template")).Single();

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest(BODY_CODE, Payload(BuildBodyWithSlots(
                BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon), originalSlotId),
                BuildSlotDto("Training 1", nameof(ComponentType.Equipment))))),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent updated = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
            .SingleAsync(component => component.Code == BODY_CODE);

        UnitTemplateComponent? fitted = await verifyContext.UnitTemplateComponents.SingleOrDefaultAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.Zero);
            Assert.That(result.SuccessValue.UnfittedTemplateNames, Is.Empty);
            Assert.That(updated.Body!.Slots, Has.Count.EqualTo(2), "Both slots are present.");
            Assert.That(fitted, Is.Not.Null, "The fitting survives.");
            Assert.That(fitted!.UnitComponentSlotId, Is.EqualTo(originalSlotId), "It still points at the original slot row.");
        }
    }

    [Test, Description("Dropping a slot through the update endpoint deletes it with its fittings, and reports both in the response.")]
    public async Task UpdateRemovingSlotDeletesFittingsAndReportsThemAsync()
    {
        // Two slots, so one can be dropped while the other proves the surviving rows keep their ids.
        await CreateAsync(BuildBodyWithSlots(
            BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon)),
            BuildSlotDto("Training 1", nameof(ComponentType.Equipment))));
        await CreateAsync(BuildWeapon(WEAPON_CODE));

        // The template fits the weapon into the first slot — the very slot the update below drops.
        List<int> slotIds = await DbFactory.FitWeaponIntoFirstSlotAsync("Fitted Template");

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest(BODY_CODE, Payload(BuildBodyWithSlots(
                BuildSlotDto("Training 1", nameof(ComponentType.Equipment), slotIds[1])))),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent updated = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
            .SingleAsync(component => component.Code == BODY_CODE);

        bool droppedSlotStillExists = await verifyContext.UnitComponentSlots.AnyAsync(slot => slot.Id == slotIds[0]);
        int fittedCount = await verifyContext.UnitTemplateComponents.CountAsync();
        bool templateStillExists = await verifyContext.UnitTemplates.AnyAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

            // The response contract the Entity Setup Tool warns the user with, and the reason this test exists:
            // the sibling bulk test proves the reconciler deletes the rows, but it asserts against a different
            // result type. This is the only cover for UnitComponentView carrying a non-zero removal.
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.EqualTo(1), "The removal is counted.");
            Assert.That(result.SuccessValue.UnfittedTemplateNames, Is.EqualTo(["Fitted Template"]), "The affected template is named back to the caller.");

            Assert.That(droppedSlotStillExists, Is.False, "The dropped slot row is deleted.");
            Assert.That(updated.Body!.Slots.Single().Id, Is.EqualTo(slotIds[1]), "The kept slot survives with its id.");
            Assert.That(fittedCount, Is.Zero, "The fitting cannot outlive the slot it named.");
            Assert.That(templateStillExists, Is.True, "Only the fitting goes — the template itself is left alone.");
        }
    }

    [Test, Description("Updating a code that doesn't exist is a not-found.")]
    public async Task UpdateUnknownCodeIsNotFoundAsync()
    {
        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();

        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest("ghost", Payload(BuildWeapon("ghost"))), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.Errors, Has.Some.Contains("No component exists with code 'ghost'"));
        }
    }

    [Test, Description("A route code disagreeing with the payload code is rejected rather than one side being quietly honoured.")]
    public async Task UpdateRouteAndPayloadCodeMismatchFailsAsync()
    {
        await CreateAsync(BuildWeapon(WEAPON_CODE));

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest(WEAPON_CODE, Payload(BuildWeapon("something_else"))), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("does not match the code in the payload"));
        }
    }

    [Test, Description("Changing an existing component's type is rejected, matching the catalogue import path.")]
    public async Task UpdateChangingComponentTypeFailsAsync()
    {
        await CreateAsync(BuildWeapon(WEAPON_CODE));

        var asArmour = new UnitComponentDto
        {
            Code = WEAPON_CODE,
            DisplayName = "Now Armour",
            ComponentType = ComponentType.Armour,
            Armour = new UnitComponentArmourDto { Resistance = new DamageProfileDto { Slash = 100 } },
        };

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest(WEAPON_CODE, Payload(asArmour)), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        UnitComponent existing = await verifyContext.UnitComponents
            .Include(component => component.Weapon)
            .Include(component => component.Armour)
            .SingleAsync(component => component.Code == WEAPON_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("cannot be changed"));
            Assert.That(existing.Weapon, Is.Not.Null, "The original extension is untouched.");
            Assert.That(existing.Armour, Is.Null, "No armour extension was written.");
        }
    }

    [Test, Description("The update endpoint reconciles the component's upgrade links — adding new ones and dropping those no longer listed.")]
    public async Task UpdateReconcilesSupersessionLinksAsync()
    {
        await CreateAsync(BuildWeapon("sword_basic"));
        await CreateAsync(BuildWeapon("sword_adv1"));
        await CreateAsync(BuildWeapon("sword_adv2"));

        // First update links the basic sword to both successors, recommending adv2.
        UpdateUnitComponentHandler firstHandler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> firstResult = await firstHandler.Handle(
            new UpdateUnitComponentRequest("sword_basic", Payload(
                BuildWeapon("sword_basic"),
                new ComponentSupersessionLinkDto { SuccessorCode = "sword_adv1" },
                new ComponentSupersessionLinkDto { SuccessorCode = "sword_adv2", IsRecommended = true })),
            CancellationToken.None);

        Assert.That(firstResult.HasSucceeded, Is.True, () => string.Join("; ", firstResult.Errors));

        // Second update keeps only adv1, now recommended — adv2's link should be dropped.
        UpdateUnitComponentHandler secondHandler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> secondResult = await secondHandler.Handle(
            new UpdateUnitComponentRequest("sword_basic", Payload(
                BuildWeapon("sword_basic"),
                new ComponentSupersessionLinkDto { SuccessorCode = "sword_adv1", IsRecommended = true })),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        int adv1Id = await verifyContext.UnitComponents
            .Where(component => component.Code == "sword_adv1")
            .Select(component => component.Id)
            .SingleAsync();

        List<ComponentSupersession> links = await verifyContext.ComponentSupersessions.ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondResult.HasSucceeded, Is.True, () => string.Join("; ", secondResult.Errors));
            Assert.That(links, Has.Count.EqualTo(1), "The surplus link to adv2 is removed.");
            Assert.That(links.Single().SuccessorComponentId, Is.EqualTo(adv1Id));
            Assert.That(links.Single().IsRecommended, Is.True, "The kept link's flag is updated.");
        }
    }

    [Test, Description("A supersession naming a component that doesn't exist is rejected and writes nothing.")]
    public async Task UpdateUnknownSuccessorFailsAsync()
    {
        await CreateAsync(BuildWeapon("sword_basic"));

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest("sword_basic", Payload(
                BuildWeapon("sword_basic") with { DisplayName = "Renamed" },
                new ComponentSupersessionLinkDto { SuccessorCode = "ghost" })),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        string persistedName = await verifyContext.UnitComponents
            .Where(component => component.Code == "sword_basic")
            .Select(component => component.DisplayName)
            .SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("unknown successor code"));
            Assert.That(persistedName, Is.EqualTo("Test Weapon"), "A rejected request writes nothing, not even the rename.");
            Assert.That(await verifyContext.ComponentSupersessions.AnyAsync(), Is.False);
        }
    }

    #region Supply requirements

    [Test, Description("Creating a Body with supply requirements persists one row per set, resolved from the set codes.")]
    public async Task CreatePersistsSupplyRequirementsAsync()
    {
        await SeedSupplySetsAsync();

        await CreateAsync(BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD, 1_500), Requirement(_MOUNT_FODDER, 4_000)]
        });

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        List<UnitComponentSupplyRequirement> persisted = await verifyContext.UnitComponentSupplyRequirements
            .Include(requirement => requirement.SupplySet)
            .Where(requirement => requirement.UnitComponent.Code == BODY_CODE)
            .OrderBy(requirement => requirement.SupplySet.Code)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persisted.Select(requirement => requirement.SupplySet.Code), Is.EqualTo(new[] { _HUMAN_FOOD, _MOUNT_FODDER }));
            Assert.That(persisted.Select(requirement => requirement.Quantity), Is.EqualTo(new[] { 1_500L, 4_000L }));
        }
    }

    [Test, Description("An update reprices a kept requirement in place and deletes the one the payload dropped.")]
    public async Task UpdateReconcilesSupplyRequirementsAsync()
    {
        await SeedSupplySetsAsync();

        await CreateAsync(BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD, 1_000), Requirement(_MOUNT_FODDER, 4_000)]
        });

        int originalFoodRowId = await RequirementRowIdAsync(_HUMAN_FOOD);

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest(BODY_CODE, Payload(BuildBodyWithSlots() with
            {
                SupplyRequirements = [Requirement(_HUMAN_FOOD, 2_000)]
            })),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        List<UnitComponentSupplyRequirement> persisted = await verifyContext.UnitComponentSupplyRequirements
            .Include(requirement => requirement.SupplySet)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(persisted, Has.Count.EqualTo(1), "The fodder requirement is gone.");
            Assert.That(persisted.Single().SupplySet.Code, Is.EqualTo(_HUMAN_FOOD));
            Assert.That(persisted.Single().Quantity, Is.EqualTo(2_000L), "The kept requirement is repriced.");
            Assert.That(persisted.Single().Id, Is.EqualTo(originalFoodRowId), "It is the same row, not a delete and re-insert.");
        }
    }

    [Test, Description("A requirement naming a supply set that doesn't exist is rejected and the component is not created.")]
    public async Task CreateUnknownSupplySetFailsAsync()
    {
        CreateUnitComponentHandler handler = await CreateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new CreateUnitComponentRequest(Payload(BuildBodyWithSlots() with
            {
                SupplyRequirements = [Requirement("NoSuchSet")]
            })),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("unknown supply set code"));
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False);
        }
    }

    [Test, Description("The editable read returns the component's requirements, which is where the designer's Supply page loads them from.")]
    public async Task GetComponentReturnsSupplyRequirementsAsync()
    {
        await SeedSupplySetsAsync();

        await CreateAsync(BuildBodyWithSlots() with
        {
            SupplyRequirements = [Requirement(_HUMAN_FOOD, 1_500)]
        });

        var handler = new GetComponentHandler(DbFactory);
        ServiceResult<EditableComponentResult> result = await handler.Handle(new GetComponentRequest(BODY_CODE), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Component.SupplyRequirements.Single().SupplySetCode, Is.EqualTo(_HUMAN_FOOD));
            Assert.That(result.SuccessValue!.Component.SupplyRequirements.Single().Quantity, Is.EqualTo(1_500));
        }
    }

    #endregion

    #region Weapon ammunition

    [Test, Description("Creating a Weapon with ammunition options persists one row per option, resolved from the asset codes.")]
    public async Task CreatePersistsWeaponAmmunitionAsync()
    {
        await SeedAmmunitionAssetsAsync();

        await CreateAsync(WeaponWithAmmunition(Ammunition(_ARROWS), Ammunition(_BULLETS)));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        List<UnitComponentWeaponAmmunition> persisted = await verifyContext.UnitComponentWeaponAmmunitions
            .Include(option => option.Asset)
            .OrderBy(option => option.Asset.Code)
            .ToListAsync();

        Assert.That(persisted.Select(option => option.Asset.Code), Is.EqualTo(new[] { _ARROWS, _BULLETS }));
    }

    [Test, Description("An update keeps the ammunition option the payload still names and deletes the one it dropped.")]
    public async Task UpdateReconcilesWeaponAmmunitionAsync()
    {
        await SeedAmmunitionAssetsAsync();

        await CreateAsync(WeaponWithAmmunition(Ammunition(_ARROWS), Ammunition(_BULLETS)));

        int originalArrowRowId = await AmmunitionRowIdAsync(_ARROWS);

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest(WEAPON_CODE, Payload(WeaponWithAmmunition(Ammunition(_ARROWS)))),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        List<UnitComponentWeaponAmmunition> persisted = await verifyContext.UnitComponentWeaponAmmunitions
            .Include(option => option.Asset)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(persisted, Has.Count.EqualTo(1), "The bullets option is gone.");
            Assert.That(persisted.Single().Asset.Code, Is.EqualTo(_ARROWS));
            Assert.That(persisted.Single().Id, Is.EqualTo(originalArrowRowId), "It is the same row, not a delete and re-insert.");
        }
    }

    [Test, Description("An ammunition option naming an Asset that doesn't exist is rejected and the component is not created.")]
    public async Task CreateUnknownAmmunitionAssetFailsAsync()
    {
        CreateUnitComponentHandler handler = await CreateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new CreateUnitComponentRequest(Payload(WeaponWithAmmunition(Ammunition("Weapons.Ammunition.NoSuchThing")))),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("unknown ammunition Asset code"));
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False);
        }
    }

    [Test, Description("Dropping a Body's inherent attack takes its ammunition options with it, rather than orphaning them against the dead weapon row.")]
    public async Task UpdateDroppingBodyWeaponRemovesItsAmmunitionAsync()
    {
        await SeedAmmunitionAssetsAsync();

        UnitComponentDto bodyWithAttack = BuildBodyWithSlots() with
        {
            Weapon = new UnitComponentWeaponDto
            {
                Attack = new DamageProfileDto { Crush = 1_000 },
                MinimumRange = 0,
                MaximumRange = 1,
                Ammunition = [Ammunition(_ARROWS)]
            }
        };

        await CreateAsync(bodyWithAttack);

        UpdateUnitComponentHandler handler = await UpdateHandlerAsync();
        ServiceResult<UnitComponentView> result = await handler.Handle(
            new UpdateUnitComponentRequest(BODY_CODE, Payload(BuildBodyWithSlots())),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(await verifyContext.UnitComponentWeapons.AnyAsync(), Is.False, "The inherent weapon is gone.");
            Assert.That(await verifyContext.UnitComponentWeaponAmmunitions.AnyAsync(), Is.False, "And so are its ammunition options.");
        }
    }

    [Test, Description("The editable read returns a weapon's ammunition options, which is where the authoring tool loads them from.")]
    public async Task GetComponentReturnsWeaponAmmunitionAsync()
    {
        await SeedAmmunitionAssetsAsync();

        await CreateAsync(WeaponWithAmmunition(Ammunition(_ARROWS)));

        var handler = new GetComponentHandler(DbFactory);
        ServiceResult<EditableComponentResult> result = await handler.Handle(new GetComponentRequest(WEAPON_CODE), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Component.Weapon!.Ammunition.Single().AssetCode, Is.EqualTo(_ARROWS));
        }
    }

    #endregion

    #region Ammunition helpers

    private const string _ARROWS = "Weapons.Ammunition.Arrows";
    private const string _BULLETS = "Weapons.Ammunition.Bullets";

    /// <summary>
    /// Inserts the two ammunition Assets these tests reference, with the meta-category and storage class an Asset row cannot do without.
    /// </summary>
    private async Task SeedAmmunitionAssetsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var metaCategory = new AssetMetaCategory { Code = "Weapons", DisplayName = "Weapons", SortOrder = 1, IconPath = "/resources/none.svg" };
        var storageClass = new StorageClass { Code = "TestArmoury", DisplayName = "Test Armoury" };

        context.AssetMetaCategories.Add(metaCategory);
        context.StorageClasses.Add(storageClass);
        await context.SaveChangesAsync();

        var category = new AssetCategory
        {
            Code = "Weapons.Ammunition",
            DisplayName = "Ammunition",
            CategoryType = AssetCategoryType.StandaloneAssets,
            SortOrder = 1,
            AssetMetaCategoryId = metaCategory.Id,
        };

        context.AssetCategories.Add(category);
        await context.SaveChangesAsync();

        int sortOrder = 0;

        foreach (string code in new[] { _ARROWS, _BULLETS })
        {
            context.Assets.Add(new Asset
            {
                Code = code,
                DisplayName = code.Split('.').Last(),
                IconPath = "/resources/none.svg",
                IsGradedAsset = false,
                SortOrder = sortOrder++,
                AssetMetaCategoryId = metaCategory.Id,
                AssetCategoryId = category.Id,
                StorageClassId = storageClass.Id,
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<int> AmmunitionRowIdAsync(string assetCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UnitComponentWeaponAmmunitions
            .Where(option => option.Asset.Code == assetCode)
            .Select(option => option.Id)
            .SingleAsync();
    }

    private static UnitComponentDto WeaponWithAmmunition(params UnitComponentWeaponAmmunitionDto[] ammunition)
    {
        UnitComponentDto weapon = BuildWeapon(WEAPON_CODE);

        return weapon with { Weapon = weapon.Weapon! with { Ammunition = ammunition } };
    }

    private static UnitComponentWeaponAmmunitionDto Ammunition(string assetCode)
    {
        return new UnitComponentWeaponAmmunitionDto { AssetCode = assetCode };
    }

    #endregion

    #region Supply helpers

    private const string _HUMAN_FOOD = "human-food";
    private const string _MOUNT_FODDER = "mount-fodder";

    /// <summary>
    /// Inserts the two supply sets these tests reference. Memberless on purpose: what a set holds is the supply-set endpoints' concern, not the component's.
    /// </summary>
    private async Task SeedSupplySetsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.SupplySets.AddRange(
            new SupplySet { Code = _HUMAN_FOOD, DisplayName = "Human Food", SupplyType = SupplyType.Food },
            new SupplySet { Code = _MOUNT_FODDER, DisplayName = "Mount Fodder", SupplyType = SupplyType.Fuel });

        await context.SaveChangesAsync();
    }

    private async Task<int> RequirementRowIdAsync(string supplySetCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UnitComponentSupplyRequirements
            .Where(requirement => requirement.SupplySet.Code == supplySetCode)
            .Select(requirement => requirement.Id)
            .SingleAsync();
    }

    private static UnitComponentSupplyRequirementDto Requirement(string supplySetCode, long quantity = 1_000)
    {
        return new UnitComponentSupplyRequirementDto
        {
            SupplySetCode = supplySetCode,
            Quantity = quantity
        };
    }

    #endregion
}
