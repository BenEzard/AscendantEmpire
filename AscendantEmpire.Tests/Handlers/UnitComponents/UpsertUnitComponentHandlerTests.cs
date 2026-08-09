using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Exercises the single-component create/update endpoints — the granular write path the Entity Setup Tool
/// uses instead of the catalogue bulk importer.
/// </summary>
/// <remarks>
/// Runs against a real SQL Server for the same reason the bulk tests do (EF complex types and 1:1 shared-PK
/// extensions that the in-memory provider does not model), and because the fitted-component foreign key these
/// tests turn on is only enforced by a real relational engine.
/// </remarks>
internal class UpsertUnitComponentHandlerTests : SqlServerTestBase
{
    private const string BODY_CODE = "test_body";
    private const string WEAPON_CODE = "test_weapon";

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
    /// Builds a body slot DTO accepting a single component type, optionally carrying an existing slot id.
    /// </summary>
    private static UnitComponentSlotDto BuildSlotDto(string label, string acceptedType, int? id = null, string? code = null)
    {
        return new UnitComponentSlotDto
        {
            Id = id,
            // Slot codes are uppercase, underscore-separated keys; default one from the label for brevity.
            Code = code ?? label.ToUpperInvariant().Replace(' ', '_'),
            DisplayName = label,
            IsVisible = true,
            Rules =
            [
                new UnitComponentSlotRuleDto
                {
                    RuleType = SlotRuleType.ComponentType,
                    Operator = SlotRuleOperator.Equals,
                    Value = acceptedType,
                },
            ],
        };
    }

    /// <summary>
    /// Builds the test body exposing exactly the given slots.
    /// </summary>
    private static UnitComponentDto BuildBodyWithSlots(params UnitComponentSlotDto[] slots)
    {
        return new UnitComponentDto
        {
            Code = BODY_CODE,
            DisplayName = "Test Body",
            ComponentType = ComponentType.Body,
            Weight = 100,
            Body = new UnitComponentBodyDto
            {
                MovementDomain = DomainType.Land,
                CampaignMovementPoints = 3000,
                BattlefieldMovementPoints = 6000,
                Slots = slots,
            },
        };
    }

    /// <summary>
    /// A standalone weapon component with the given code.
    /// </summary>
    private static UnitComponentDto BuildWeapon(string code)
    {
        return new UnitComponentDto
        {
            Code = code,
            DisplayName = "Test Weapon",
            ComponentType = ComponentType.Weapon,
            Weight = 10,
            Weapon = new UnitComponentWeaponDto
            {
                Attack = new DamageProfileDto { Slash = 1000 },
                MinimumRange = 1,
                MaximumRange = 4,
            },
        };
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
            Assert.That(result.SuccessValue.SlotIds, Is.EqualTo(new[] { persisted.Body!.Slots.Single().Id }), "The saved slot ids come back for the client to reuse.");
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

        int originalSlotId;

        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            UnitComponent body = await seedContext.UnitComponents
                .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
                .SingleAsync(component => component.Code == BODY_CODE);

            int weaponId = await seedContext.UnitComponents
                .Where(component => component.Code == WEAPON_CODE)
                .Select(component => component.Id)
                .SingleAsync();

            originalSlotId = body.Body!.Slots.Single().Id;

            seedContext.UnitTemplates.Add(new UnitTemplate
            {
                DisplayName = "Fitted Template",
                MapImagePath = "test.png",
                BodyComponentId = body.Id,
                Components = [new UnitTemplateComponent { UnitComponentSlotId = originalSlotId, UnitComponentId = weaponId }],
            });

            await seedContext.SaveChangesAsync();
        }

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
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.EqualTo(0));
            Assert.That(result.SuccessValue.UnfittedTemplateNames, Is.Empty);
            Assert.That(updated.Body!.Slots, Has.Count.EqualTo(2), "Both slots are present.");
            Assert.That(fitted, Is.Not.Null, "The fitting survives.");
            Assert.That(fitted!.UnitComponentSlotId, Is.EqualTo(originalSlotId), "It still points at the original slot row.");
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
}
