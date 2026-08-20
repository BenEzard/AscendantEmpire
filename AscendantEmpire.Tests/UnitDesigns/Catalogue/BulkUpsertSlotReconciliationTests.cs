using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;
using AscendantEmpire.Tests.UnitDesigns.Shared;

using static AscendantEmpire.Tests.UnitDesigns.Shared.UnitDesignPayloads;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Exercises how <see cref="BulkUpsertUnitComponentsHandler"/> reconciles a body's slots across a
/// re-import: which slot rows survive with their ids, which are deleted, and what happens to the saved
/// templates fitted into them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BodySlotsRoundTripAndReplaceAsync"/> is deliberately first. It is the unfitted baseline — no
/// slot ids in the payload and no template fitted — where a re-import simply replaces the slot rows
/// wholesale. Every test after it qualifies that baseline by adding slot ids, a fitted template, or both,
/// so reading them in order is reading the behaviour from simplest to hardest.
/// </para>
/// <para>
/// Tier 3 (containerised SQL Server): the arrangement these tests turn on is a template fitted into a slot,
/// and the <c>FK_UnitTemplateComponent_UnitComponentSlot_UnitComponentSlotId</c> constraint that makes a
/// slot delete interesting is only enforced by a real relational engine. See <c>docs/testing.md</c>.
/// </para>
/// </remarks>
internal class BulkUpsertSlotReconciliationTests : SqlServerTestBase
{
    private const string BODY_CODE = UnitDesignPayloads.BODY_CODE;
    private const string WEAPON_CODE = UnitDesignPayloads.WEAPON_CODE;

    [Test, Description("A body's slots round-trip on insert and are replaced (not appended) on re-import.")]
    public async Task BodySlotsRoundTripAndReplaceAsync()
    {
        // A slot's UI position is a normalized offset from the image center, so it carries fractional
        // values that must round-trip exactly through the decimal column.
        UnitComponentSlotDto Slot(string label, string acceptedType, decimal x = 0m, decimal y = 0m) => new()
        {
            Code = label.ToUpperInvariant(),
            DisplayName = label,
            IsVisible = true,
            UIXPosition = x,
            UIYPosition = y,
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

        // A body exposing a single slot. Only bodies carry slots, so they ride on the body extension.
        UnitComponentDto BodyWithSlot(UnitComponentSlotDto slot) => new()
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
                Slots = [slot],
            },
        };

        UnitComponentDto bodyWithSlot = BodyWithSlot(Slot("Module", nameof(ComponentType.Equipment), -0.9m, 0.4m));

        BulkUpsertUnitComponentsHandler insertHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await insertHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [bodyWithSlot] }),
            CancellationToken.None);

        await using (AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync())
        {
            UnitComponent body = await verify.UnitComponents
                .Include(c => c.Body!).ThenInclude(b => b.Slots).ThenInclude(s => s.Rules)
                .SingleAsync(c => c.Code == BODY_CODE);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Body!.Slots, Has.Count.EqualTo(1), "The body's slot should persist.");
                Assert.That(body.Body.Slots.Single().DisplayName, Is.EqualTo("Module"));
                Assert.That(body.Body.Slots.Single().UIXPosition, Is.EqualTo(-0.9m), "Fractional X position round-trips.");
                Assert.That(body.Body.Slots.Single().UIYPosition, Is.EqualTo(0.4m), "Fractional Y position round-trips.");
                Assert.That(body.Body.Slots.Single().Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Equipment)));
            }
        }

        // Re-import with a different slot — the old slot (and its rules) should be replaced, not appended.
        UnitComponentDto bodyReimport = BodyWithSlot(Slot("Scope", nameof(ComponentType.Weapon)));

        BulkUpsertUnitComponentsHandler updateHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await updateHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [bodyReimport] }),
            CancellationToken.None);

        await using (AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync())
        {
            UnitComponent body = await verify.UnitComponents
                .Include(c => c.Body!).ThenInclude(b => b.Slots).ThenInclude(s => s.Rules)
                .SingleAsync(c => c.Code == BODY_CODE);
            int totalSlotRows = await verify.UnitComponentSlots.CountAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Body!.Slots, Has.Count.EqualTo(1), "Still one slot after re-import.");
                Assert.That(body.Body.Slots.Single().DisplayName, Is.EqualTo("Scope"), "The slot was replaced from the payload.");
                Assert.That(totalSlotRows, Is.EqualTo(1), "The old slot row was deleted, not left orphaned.");
            }
        }
    }

    /// <summary>
    /// Seeds the test body (with the given slots) plus a weapon, then saves a global template that fits the
    /// weapon into the body's first slot — the arrangement that makes a slot delete hit the fitted-row FK.
    /// </summary>
    /// <returns>The ids of the body's slots, in creation order.</returns>
    private async Task<List<int>> SeedBodyWithFittedTemplateAsync(string templateName, params UnitComponentSlotDto[] slots)
    {
        BulkUpsertUnitComponentsHandler seedHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> seeded = await seedHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto
            {
                Components = [BuildBodyWithSlots(slots), BuildWeapon(WEAPON_CODE)],
            }),
            CancellationToken.None);

        Assert.That(seeded.HasSucceeded, Is.True, () => string.Join("; ", seeded.Errors));

        return await DbFactory.FitWeaponIntoFirstSlotAsync(templateName);
    }

    [Test, Description("Adding a slot to a body whose slots are already fitted into a saved template succeeds and leaves the existing slot rows and fittings intact.")]
    public async Task AddingSlotToFittedBodyPreservesExistingSlotsAndFittingsAsync()
    {
        // The reported failure: the delete-all reconciliation tripped
        // FK_UnitTemplateComponent_UnitComponentSlot_UnitComponentSlotId as soon as any template was fitted.
        List<int> slotIds = await SeedBodyWithFittedTemplateAsync("Fitted Template", BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon)));
        int originalSlotId = slotIds.Single();

        // Re-submit the existing slot (by id) plus a brand-new one, exactly as the dialog now does.
        UnitComponentDto withExtraSlot = BuildBodyWithSlots(
            BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon), originalSlotId),
            BuildSlotDto("Training 1", nameof(ComponentType.Equipment)));

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [withExtraSlot] }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent body = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
            .SingleAsync(component => component.Code == BODY_CODE);

        List<UnitComponentSlot> orderedSlots = [.. body.Body!.Slots.OrderBy(slot => slot.Id)];
        UnitTemplateComponent? fitted = await verifyContext.UnitTemplateComponents.SingleOrDefaultAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.EqualTo(0), "Adding a slot removes nothing.");
            Assert.That(result.SuccessValue.UnfittedTemplateNames, Is.Empty, "No template should lose a fitting.");

            Assert.That(orderedSlots, Has.Count.EqualTo(2), "The body should now expose both slots.");
            Assert.That(orderedSlots[0].Id, Is.EqualTo(originalSlotId), "The existing slot keeps its row, so template fittings stay valid.");
            Assert.That(orderedSlots[0].DisplayName, Is.EqualTo("Weapon 1"));
            Assert.That(orderedSlots[1].DisplayName, Is.EqualTo("Training 1"), "The new slot is appended after the existing one.");

            Assert.That(fitted, Is.Not.Null, "The template's fitted component should survive untouched.");
            Assert.That(fitted!.UnitComponentSlotId, Is.EqualTo(originalSlotId), "The fitting still points at the same slot.");
        }
    }

    [Test, Description("Editing a slot updates its row in place, keeping the id its fitted template components reference.")]
    public async Task EditingSlotUpdatesRowInPlaceAsync()
    {
        List<int> slotIds = await SeedBodyWithFittedTemplateAsync("Fitted Template", BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon)));
        int originalSlotId = slotIds.Single();

        // Rename the slot and re-point its acceptance rule, carrying the id through.
        UnitComponentDto renamedSlot = BuildBodyWithSlots(BuildSlotDto("Main Hand", nameof(ComponentType.Equipment), originalSlotId));

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [renamedSlot] }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent body = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots).ThenInclude(slot => slot.Rules)
            .SingleAsync(component => component.Code == BODY_CODE);

        UnitComponentSlot slot = body.Body!.Slots.Single();
        int totalSlotRows = await verifyContext.UnitComponentSlots.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.EqualTo(0), "An edit is not a removal.");
            Assert.That(slot.Id, Is.EqualTo(originalSlotId), "The slot is updated in place, not replaced.");
            Assert.That(slot.DisplayName, Is.EqualTo("Main Hand"), "Scalars are overwritten from the payload.");
            Assert.That(slot.Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Equipment)), "Rules are replaced from the payload.");
            Assert.That(totalSlotRows, Is.EqualTo(1), "No extra slot row was written.");
        }
    }

    [Test, Description("Removing a slot deletes it along with the fitted template components that referenced it, and reports the affected templates.")]
    public async Task RemovingSlotDeletesItsFittedTemplateComponentsAsync()
    {
        List<int> slotIds = await SeedBodyWithFittedTemplateAsync(
            "Fitted Template",
            BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon)),
            BuildSlotDto("Training 1", nameof(ComponentType.Equipment)));

        // Drop the first slot (the fitted one), keeping the second.
        UnitComponentDto withoutFittedSlot = BuildBodyWithSlots(BuildSlotDto("Training 1", nameof(ComponentType.Equipment), slotIds[1]));

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [withoutFittedSlot] }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent body = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
            .SingleAsync(component => component.Code == BODY_CODE);

        bool removedSlotStillExists = await verifyContext.UnitComponentSlots.AnyAsync(slot => slot.Id == slotIds[0]);
        int fittedCount = await verifyContext.UnitTemplateComponents.CountAsync();
        bool templateStillExists = await verifyContext.UnitTemplates.AnyAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(removedSlotStillExists, Is.False, "The removed slot row is deleted.");
            Assert.That(body.Body!.Slots.Single().Id, Is.EqualTo(slotIds[1]), "The kept slot survives with its id.");
            Assert.That(fittedCount, Is.EqualTo(0), "The fitting cannot outlive the slot it named.");
            Assert.That(templateStillExists, Is.True, "Only the fitting goes — the template itself is left alone.");

            // The delete is intended but must not be silent.
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.EqualTo(1), "The removal is counted.");
            Assert.That(result.SuccessValue.UnfittedTemplateNames, Is.EqualTo(new[] { "Fitted Template" }), "The affected template is reported back.");
        }
    }

    [Test, Description("Re-importing a catalogue whose slots carry no ids matches them by code, leaving the existing slot rows and their template fittings intact.")]
    public async Task ReimportWithoutSlotIdsMatchesOnCodeAsync()
    {
        List<int> slotIds = await SeedBodyWithFittedTemplateAsync("Fitted Template", BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon)));
        int originalSlotId = slotIds.Single();

        // A catalogue exported from another database carries no slot ids — they would be meaningless here.
        // Matching falls back to the code, which is exactly what makes the import non-destructive.
        UnitComponentDto reimported = BuildBodyWithSlots(
            BuildSlotDto("Weapon Mount", nameof(ComponentType.Weapon), id: null, code: "WEAPON_1"));

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [reimported] }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent body = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
            .SingleAsync(component => component.Code == BODY_CODE);

        UnitComponentSlot slot = body.Body!.Slots.Single();
        UnitTemplateComponent? fitted = await verifyContext.UnitTemplateComponents.SingleOrDefaultAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.EqualTo(0), "Nothing is removed — the code matched.");
            Assert.That(slot.Id, Is.EqualTo(originalSlotId), "The existing row is updated in place, not recreated.");
            Assert.That(slot.DisplayName, Is.EqualTo("Weapon Mount"), "The imported values are applied.");
            Assert.That(fitted, Is.Not.Null, "The template's fitted component survives the re-import.");
            Assert.That(fitted!.UnitComponentSlotId, Is.EqualTo(originalSlotId));
        }
    }

    [Test, Description("Renaming a slot's code while sending its id updates the row in place, rather than reading as a remove plus an add.")]
    public async Task RenamingSlotCodeWithIdUpdatesInPlaceAsync()
    {
        List<int> slotIds = await SeedBodyWithFittedTemplateAsync("Fitted Template", BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon)));
        int originalSlotId = slotIds.Single();

        // The dev tool always round-trips the id, which is the only thing distinguishing a rename from a
        // slot being deleted and an unrelated one added.
        UnitComponentDto renamed = BuildBodyWithSlots(
            BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon), id: originalSlotId, code: "MAIN_HAND"));

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [renamed] }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponentSlot slot = await verifyContext.UnitComponentSlots.SingleAsync();
        UnitTemplateComponent? fitted = await verifyContext.UnitTemplateComponents.SingleOrDefaultAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.RemovedSlotCount, Is.EqualTo(0), "A rename removes nothing.");
            Assert.That(slot.Id, Is.EqualTo(originalSlotId), "Same row.");
            Assert.That(slot.Code, Is.EqualTo("MAIN_HAND"), "The code is updated.");
            Assert.That(fitted, Is.Not.Null, "The fitting is undisturbed by the rename.");
        }
    }

    [Test, Description("Two slots sharing a code are rejected with a usable message rather than an opaque unique-index violation.")]
    public async Task DuplicateSlotCodesAreRejectedAsync()
    {
        UnitComponentDto body = BuildBodyWithSlots(
            BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon), id: null, code: "WEAPON_1"),
            BuildSlotDto("Weapon 2", nameof(ComponentType.Weapon), id: null, code: "WEAPON_1"));

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [body] }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("duplicate slot code"));
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "Nothing should be written.");
        }
    }

    [Test, Description("A slot id that doesn't belong to the target component is rejected rather than silently inserted as a new slot.")]
    public async Task SlotIdFromAnotherComponentIsRejectedAsync()
    {
        List<int> slotIds = await SeedBodyWithFittedTemplateAsync("Fitted Template", BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon)));
        int foreignSlotId = slotIds.Single() + 1000;

        UnitComponentDto withForeignSlotId = BuildBodyWithSlots(BuildSlotDto("Weapon 1", nameof(ComponentType.Weapon), foreignSlotId));

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [withForeignSlotId] }),
            CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent body = await verifyContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
            .SingleAsync(component => component.Code == BODY_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("does not belong to it"));
            Assert.That(body.Body!.Slots.Single().Id, Is.EqualTo(slotIds.Single()), "A rejected request writes nothing.");
            Assert.That(await verifyContext.UnitTemplateComponents.CountAsync(), Is.EqualTo(1), "The existing fitting is untouched.");
        }
    }
}
