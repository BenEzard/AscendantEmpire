using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Exercises <see cref="BulkUpsertUnitComponentsHandler"/> against a real SQL Server database — the
/// catalog relies on EF complex types (DamageProfile) and 1:1 shared-PK extensions that the in-memory
/// provider does not model correctly.
/// </summary>
internal class BulkUpsertUnitComponentsHandlerTests : SqlServerTestBase
{
    private const string BODY_CODE = "test_body";
    private const string WEAPON_CODE = "test_weapon";
    private const string ARMOUR_CODE = "test_armour";
    private const string TAG_CODE = "amphibious";

    private async Task<BulkUpsertUnitComponentsHandler> CreateHandlerAsync()
    {
        AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        return new BulkUpsertUnitComponentsHandler(new NullLogger<BulkUpsertUnitComponentsHandler>(), context);
    }

    [Test, Description("Inserting a full component graph persists the parent, its 1:1 extension, child rows and tag joins.")]
    public async Task FullGraphInsertPersistsEntireGraphAsync()
    {
        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();

        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(BuildFullCatalog()), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent body = await verifyContext.UnitComponents
            .Include(c => c.Body)
            .Include(c => c.Slots).ThenInclude(s => s.Rules)
            .Include(c => c.Tags)
            .SingleAsync(c => c.Code == BODY_CODE);

        UnitComponent weapon = await verifyContext.UnitComponents
            .Include(c => c.Weapon!).ThenInclude(w => w.DomainModifiers)
            .SingleAsync(c => c.Code == WEAPON_CODE);

        UnitComponent armour = await verifyContext.UnitComponents
            .Include(c => c.Armour)
            .SingleAsync(c => c.Code == ARMOUR_CODE);

        using (Assert.EnterMultipleScope())
        {
            // --- Result summary ---
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created), "Should report Created.");
            Assert.That(result.SuccessValue, Is.Not.Null);
            Assert.That(result.SuccessValue!.AddedCount, Is.EqualTo(3), "All three components are new.");
            Assert.That(result.SuccessValue.UpdatedCount, Is.EqualTo(0), "Nothing should be updated.");

            // --- Body extension shares the parent PK; slots (now on the component) + rules ---
            Assert.That(body.Body, Is.Not.Null);
            Assert.That(body.Body!.UnitComponentId, Is.EqualTo(body.Id), "Body extension shares the component PK.");
            Assert.That(body.Body.MovementDomain, Is.EqualTo(DomainType.Land));
            Assert.That(body.Slots, Has.Count.EqualTo(1));
            Assert.That(body.Slots.Single().Rules, Has.Count.EqualTo(1));
            Assert.That(body.Slots.Single().Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Weapon)));

            // --- Tag join ---
            Assert.That(body.Tags, Has.Count.EqualTo(1), "Body should carry one tag join.");

            // --- Main-table Detection/Visibility round-trip ---
            Assert.That(weapon.Detection, Is.EqualTo(2000), "Detection should round-trip on insert.");
            Assert.That(weapon.Visibility, Is.EqualTo(3000), "Visibility should round-trip on insert.");

            // --- Weapon + complex Attack profile + domain modifier ---
            Assert.That(weapon.Weapon, Is.Not.Null);
            Assert.That(weapon.Weapon!.UnitComponentId, Is.EqualTo(weapon.Id), "Weapon extension shares the component PK.");
            Assert.That(weapon.Weapon.Attack.Slash, Is.EqualTo(1000), "Attack damage profile should round-trip.");
            Assert.That(weapon.Weapon.MinimumRange, Is.EqualTo(1));
            Assert.That(weapon.Weapon.MaximumRange, Is.EqualTo(4));
            Assert.That(weapon.Weapon.DomainModifiers, Has.Count.EqualTo(1));
            Assert.That(weapon.Weapon.DomainModifiers.Single().Domain, Is.EqualTo(DomainType.Land));

            // --- Armour + complex Resistance profile ---
            Assert.That(armour.Armour, Is.Not.Null);
            Assert.That(armour.Armour!.UnitComponentId, Is.EqualTo(armour.Id), "Armour extension shares the component PK.");
            Assert.That(armour.Armour.Resistance.Pierce, Is.EqualTo(250), "Resistance damage profile should round-trip.");

            // --- The shared tag row ---
            Assert.That(verifyContext.Tags.Count(t => t.Code == TAG_CODE), Is.EqualTo(1), "Exactly one tag row created.");
        }
    }

    [Test, Description("Re-running with a changed payload updates every existing component in place — overwriting scalars and replacing children — and writes no duplicates.")]
    public async Task RerunUpdatesExistingComponentsByCodeAsync()
    {
        // First run inserts everything.
        BulkUpsertUnitComponentsHandler firstHandler = await CreateHandlerAsync();
        await firstHandler.Handle(new BulkUpsertUnitComponentsRequest(BuildFullCatalog()), CancellationToken.None);

        // Capture the original surrogate ids so we can prove the rows were updated, not replaced.
        await using (AscendantEmpireContext beforeContext = await DbFactory.CreateDbContextAsync())
        {
            int originalWeaponId = await beforeContext.UnitComponents.Where(c => c.Code == WEAPON_CODE).Select(c => c.Id).SingleAsync();
            Assert.That(originalWeaponId, Is.GreaterThan(0));
        }

        // Build a mutated catalog: rename the weapon, change its attack, and swap the body's slot.
        BulkUpsertUnitComponentsDto changed = BuildFullCatalog();
        UnitComponentDto changedWeapon = changed.Components.Single(c => c.Code == WEAPON_CODE) with
        {
            DisplayName = "Upgraded Weapon",
            Detection = 5000,
            Visibility = 6000,
            Weapon = new UnitComponentWeaponDto
            {
                Attack = new DamageProfileDto { Slash = 7777 },
                MinimumRange = 2,
                MaximumRange = 9,
                DomainModifiers = [new UnitComponentWeaponDomainModifierDto { Domain = DomainType.Water, DamageMultiplier = 2000 }],
            },
        };

        var changedCatalog = new BulkUpsertUnitComponentsDto
        {
            Tags = changed.Tags,
            Components = [.. changed.Components.Select(c => c.Code == WEAPON_CODE ? changedWeapon : c)],
        };

        // Second run upserts the mutated payload.
        BulkUpsertUnitComponentsHandler secondHandler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await secondHandler.Handle(new BulkUpsertUnitComponentsRequest(changedCatalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int componentCount = await verifyContext.UnitComponents.CountAsync();
        int tagCount = await verifyContext.Tags.CountAsync();

        UnitComponent weapon = await verifyContext.UnitComponents
            .Include(c => c.Weapon!).ThenInclude(w => w.DomainModifiers)
            .SingleAsync(c => c.Code == WEAPON_CODE);

        using (Assert.EnterMultipleScope())
        {
            // --- Result summary: everything updated, nothing added; 200 because existing rows were touched. ---
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success), "Should report 200 when existing rows are updated.");
            Assert.That(result.SuccessValue!.AddedCount, Is.EqualTo(0), "Nothing new on the second run.");
            Assert.That(result.SuccessValue.UpdatedCount, Is.EqualTo(3), "All three are updated.");
            Assert.That(result.SuccessValue.UpdatedCodes, Is.EquivalentTo(new[] { BODY_CODE, WEAPON_CODE, ARMOUR_CODE }));

            // --- No duplicate rows written. ---
            Assert.That(componentCount, Is.EqualTo(3), "No duplicate components written.");
            Assert.That(tagCount, Is.EqualTo(1), "No duplicate tag written.");

            // --- The weapon's scalars and children reflect the new payload. ---
            Assert.That(weapon.DisplayName, Is.EqualTo("Upgraded Weapon"), "Scalar field should be overwritten.");
            Assert.That(weapon.Detection, Is.EqualTo(5000), "Detection should be overwritten on update.");
            Assert.That(weapon.Visibility, Is.EqualTo(6000), "Visibility should be overwritten on update.");
            Assert.That(weapon.Weapon!.Attack.Slash, Is.EqualTo(7777), "Complex attack profile should be overwritten.");
            Assert.That(weapon.Weapon.MaximumRange, Is.EqualTo(9));
            Assert.That(weapon.Weapon.DomainModifiers, Has.Count.EqualTo(1), "Old modifier replaced, not appended.");
            Assert.That(weapon.Weapon.DomainModifiers.Single().Domain, Is.EqualTo(DomainType.Water), "Child collection should be replaced from the payload.");
        }
    }

    [Test, Description("A payload mixing an existing code with a new one updates the existing component and inserts the new one.")]
    public async Task PartialOverlapUpdatesExistingAndInsertsNewAsync()
    {
        // Seed a single weapon.
        BulkUpsertUnitComponentsHandler seedHandler = await CreateHandlerAsync();
        await seedHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [BuildWeapon(WEAPON_CODE)] }),
            CancellationToken.None);

        // Re-submit the same weapon (renamed) plus a brand-new one.
        UnitComponentDto renamedWeapon = BuildWeapon(WEAPON_CODE) with { DisplayName = "Renamed Weapon" };

        var mixedCatalog = new BulkUpsertUnitComponentsDto
        {
            Components = [renamedWeapon, BuildWeapon("new_weapon")],
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(mixedCatalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int weaponCount = await verifyContext.UnitComponents.CountAsync();
        string existingName = await verifyContext.UnitComponents.Where(c => c.Code == WEAPON_CODE).Select(c => c.DisplayName).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.AddedCount, Is.EqualTo(1), "Only the new weapon is inserted.");
            Assert.That(result.SuccessValue.UpdatedCount, Is.EqualTo(1));
            Assert.That(result.SuccessValue.UpdatedCodes.Single(), Is.EqualTo(WEAPON_CODE));
            Assert.That(weaponCount, Is.EqualTo(2), "Existing weapon plus the new one — no duplicate.");
            Assert.That(existingName, Is.EqualTo("Renamed Weapon"), "Existing weapon should be updated in place.");
        }
    }

    [Test, Description("Re-submitting an existing code with a different ComponentType is rejected and writes nothing.")]
    public async Task ChangingComponentTypeFailsAsync()
    {
        // Seed a weapon.
        BulkUpsertUnitComponentsHandler seedHandler = await CreateHandlerAsync();
        await seedHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [BuildWeapon(WEAPON_CODE)] }),
            CancellationToken.None);

        // Re-submit the same code as an Armour component.
        var asArmour = new UnitComponentDto
        {
            Code = WEAPON_CODE,
            DisplayName = "Now Armour",
            ComponentType = ComponentType.Armour,
            Armour = new UnitComponentArmourDto { Resistance = new DamageProfileDto { Slash = 100 } },
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(
                new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [asArmour] }),
                CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        UnitComponent existing = await verifyContext.UnitComponents
            .Include(c => c.Weapon)
            .Include(c => c.Armour)
            .SingleAsync(c => c.Code == WEAPON_CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("cannot be changed")), Is.True);
            Assert.That(existing.ComponentType, Is.EqualTo(ComponentType.Weapon), "Original type is unchanged.");
            Assert.That(existing.Weapon, Is.Not.Null, "Original weapon extension is untouched.");
            Assert.That(existing.Armour, Is.Null, "No armour extension was written.");
        }
    }

    [Test, Description("A component whose extension doesn't match its ComponentType fails and writes nothing.")]
    public async Task TypeExtensionMismatchFailsAsync()
    {
        // Declared as Armour but carries Weapon data (and no Armour data).
        var badComponent = new UnitComponentDto
        {
            Code = "mismatch",
            DisplayName = "Mismatch",
            ComponentType = ComponentType.Armour,
            Weapon = new UnitComponentWeaponDto { Attack = new DamageProfileDto(), MinimumRange = 1, MaximumRange = 1 },
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(
                new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [badComponent] }),
                CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "Nothing should be written.");
        }
    }

    [Test, Description("Two components sharing a Code fail validation and write nothing.")]
    public async Task DuplicateCodeInPayloadFailsAsync()
    {
        var catalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("dup"), BuildWeapon("dup")],
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("Duplicate component code")), Is.True);
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "Nothing should be written.");
        }
    }

    [Test, Description("A component referencing a tag absent from the payload and the DB fails and writes nothing.")]
    public async Task UnknownTagReferenceFailsAsync()
    {
        var component = BuildWeapon(WEAPON_CODE) with { TagCodes = ["ghost"] };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(
                new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [component] }),
                CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("unknown tag code")), Is.True);
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "Nothing should be written.");
        }
    }

    [Test, Description("A valid supersession pair creates a directed link with its IsRecommended flag.")]
    public async Task SupersessionPairCreatesLinkAsync()
    {
        var catalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword_basic"), BuildWeapon("sword_advanced")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_advanced", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int predecessorId = await verifyContext.UnitComponents.Where(c => c.Code == "sword_basic").Select(c => c.Id).SingleAsync();
        int successorId = await verifyContext.UnitComponents.Where(c => c.Code == "sword_advanced").Select(c => c.Id).SingleAsync();
        ComponentSupersession? link = await verifyContext.ComponentSupersessions.SingleOrDefaultAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(link, Is.Not.Null, "The supersession link should be persisted.");
            Assert.That(link!.PredecessorComponentId, Is.EqualTo(predecessorId));
            Assert.That(link.SuccessorComponentId, Is.EqualTo(successorId));
            Assert.That(link.IsRecommended, Is.True, "IsRecommended should round-trip.");
        }
    }

    [Test, Description("Re-importing a predecessor replaces its links with exactly those submitted — surplus removed, kept ones updated.")]
    public async Task ReimportReplacesPredecessorLinksAsync()
    {
        // First import: basic is superseded by two successors, with adv2 recommended.
        var firstCatalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword_basic"), BuildWeapon("sword_adv1"), BuildWeapon("sword_adv2")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_adv1" },
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_adv2", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler firstHandler = await CreateHandlerAsync();
        await firstHandler.Handle(new BulkUpsertUnitComponentsRequest(firstCatalog), CancellationToken.None);

        // Second import re-submits only the predecessor and a single link to adv1 (now recommended).
        // adv1 resolves from the database even though it isn't in this payload's Components list.
        var secondCatalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword_basic")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_adv1", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler secondHandler = await CreateHandlerAsync();
        await secondHandler.Handle(new BulkUpsertUnitComponentsRequest(secondCatalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int predecessorId = await verifyContext.UnitComponents.Where(c => c.Code == "sword_basic").Select(c => c.Id).SingleAsync();
        int adv1Id = await verifyContext.UnitComponents.Where(c => c.Code == "sword_adv1").Select(c => c.Id).SingleAsync();

        List<ComponentSupersession> links = await verifyContext.ComponentSupersessions
            .Where(s => s.PredecessorComponentId == predecessorId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(links, Has.Count.EqualTo(1), "The surplus link (to adv2) should be removed.");
            Assert.That(links.Single().SuccessorComponentId, Is.EqualTo(adv1Id), "Only the adv1 link should remain.");
            Assert.That(links.Single().IsRecommended, Is.True, "The kept link's flag should be updated from the payload.");
        }
    }

    [Test, Description("A component that supersedes itself is rejected and writes nothing.")]
    public async Task SelfSupersessionFailsAsync()
    {
        var catalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword", SuccessorCode = "sword" },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("cannot supersede itself")), Is.True);
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "Nothing should be written.");
            Assert.That(await verifyContext.ComponentSupersessions.AnyAsync(), Is.False, "No links should be written.");
        }
    }

    [Test, Description("A supersession referencing a code absent from the payload and the DB is rejected and writes nothing.")]
    public async Task UnknownSupersessionCodeFailsAsync()
    {
        var catalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword", SuccessorCode = "ghost" },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("unknown successor code")), Is.True);
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "Nothing should be written.");
        }
    }

    [Test, Description("Two identical supersession pairs are rejected and write nothing.")]
    public async Task DuplicateSupersessionPairFailsAsync()
    {
        var catalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("a"), BuildWeapon("b")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "b" },
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "b" },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("Duplicate component supersession pair")), Is.True);
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "Nothing should be written.");
        }
    }

    [Test, Description("Flagging more than one recommended successor for a predecessor is rejected and writes nothing.")]
    public async Task MultipleRecommendedSuccessorsFailAsync()
    {
        var catalog = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("a"), BuildWeapon("b"), BuildWeapon("c")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "b", IsRecommended = true },
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "c", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await CreateHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("More than one recommended successor")), Is.True);
            Assert.That(await verifyContext.ComponentSupersessions.AnyAsync(), Is.False, "No links should be written.");
        }
    }

    [Test, Description("A tag's TagType is set on creation and overwritten when the same code is re-imported.")]
    public async Task TagTypeIsSetOnCreateAndUpdatedOnReimportAsync()
    {
        // First import classifies "sword" as a Classification tag.
        var firstCatalog = new BulkUpsertUnitComponentsDto
        {
            Tags = [new TagDto { Code = "sword", DisplayName = "Sword", TagType = TagType.Classification }],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler firstHandler = await CreateHandlerAsync();
        await firstHandler.Handle(new BulkUpsertUnitComponentsRequest(firstCatalog), CancellationToken.None);

        await using (AscendantEmpireContext afterCreate = await DbFactory.CreateDbContextAsync())
        {
            TagType created = await afterCreate.Tags.Where(t => t.Code == "sword").Select(t => t.TagType).SingleAsync();
            Assert.That(created, Is.EqualTo(TagType.Classification), "TagType should be set on creation.");
        }

        // Re-import the same tag code re-classified as a Buff.
        var secondCatalog = new BulkUpsertUnitComponentsDto
        {
            Tags = [new TagDto { Code = "sword", DisplayName = "Sword", TagType = TagType.Buff }],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler secondHandler = await CreateHandlerAsync();
        await secondHandler.Handle(new BulkUpsertUnitComponentsRequest(secondCatalog), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int tagCount = await verifyContext.Tags.CountAsync(t => t.Code == "sword");
        TagType updated = await verifyContext.Tags.Where(t => t.Code == "sword").Select(t => t.TagType).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tagCount, Is.EqualTo(1), "No duplicate tag should be created on re-import.");
            Assert.That(updated, Is.EqualTo(TagType.Buff), "TagType should be overwritten on re-import.");
        }
    }

    [Test, Description("A non-body component (a weapon) can carry slots; they round-trip on insert and are replaced on re-import.")]
    public async Task NonBodyComponentSlotsRoundTripAndReplaceAsync()
    {
        // A weapon that itself exposes a slot accepting Equipment (e.g. a targeting module).
        UnitComponentSlotDto Slot(string label, string acceptedType) => new()
        {
            DisplayName = label,
            IsVisible = true,
            UIXPosition = 0,
            UIYPosition = 0,
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

        UnitComponentDto weaponWithSlot = BuildWeapon(WEAPON_CODE) with { Slots = [Slot("Module", nameof(ComponentType.Equipment))] };

        BulkUpsertUnitComponentsHandler insertHandler = await CreateHandlerAsync();
        await insertHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [weaponWithSlot] }),
            CancellationToken.None);

        await using (AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync())
        {
            UnitComponent weapon = await verify.UnitComponents
                .Include(c => c.Slots).ThenInclude(s => s.Rules)
                .SingleAsync(c => c.Code == WEAPON_CODE);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(weapon.Slots, Has.Count.EqualTo(1), "The weapon's slot should persist.");
                Assert.That(weapon.Slots.Single().DisplayName, Is.EqualTo("Module"));
                Assert.That(weapon.Slots.Single().Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Equipment)));
            }
        }

        // Re-import with a different slot — the old slot (and its rules) should be replaced, not appended.
        UnitComponentDto weaponReimport = BuildWeapon(WEAPON_CODE) with { Slots = [Slot("Scope", nameof(ComponentType.Weapon))] };

        BulkUpsertUnitComponentsHandler updateHandler = await CreateHandlerAsync();
        await updateHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [weaponReimport] }),
            CancellationToken.None);

        await using (AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync())
        {
            UnitComponent weapon = await verify.UnitComponents
                .Include(c => c.Slots).ThenInclude(s => s.Rules)
                .SingleAsync(c => c.Code == WEAPON_CODE);
            int totalSlotRows = await verify.UnitComponentSlots.CountAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(weapon.Slots, Has.Count.EqualTo(1), "Still one slot after re-import.");
                Assert.That(weapon.Slots.Single().DisplayName, Is.EqualTo("Scope"), "The slot was replaced from the payload.");
                Assert.That(totalSlotRows, Is.EqualTo(1), "The old slot row was deleted, not left orphaned.");
            }
        }
    }

    // --- Payload builders -------------------------------------------------------------------

    /// <summary>
    /// A catalog with one of each extension type plus a tag referenced by the body.
    /// </summary>
    private static BulkUpsertUnitComponentsDto BuildFullCatalog()
    {
        return new BulkUpsertUnitComponentsDto
        {
            Tags = [new TagDto { Code = TAG_CODE, DisplayName = "Amphibious", TagType = TagType.Buff }],
            Components =
            [
                new UnitComponentDto
                {
                    Code = BODY_CODE,
                    DisplayName = "Test Body",
                    ComponentType = ComponentType.Body,
                    Weight = 100,
                    TagCodes = [TAG_CODE],
                    Body = new UnitComponentBodyDto
                    {
                        MovementDomain = DomainType.Land,
                        CampaignMovementPoints = 3m,
                        BattlefieldMovementPoints = 6m,
                        MinStrength = 1,
                        MaxStrength = 10,
                        IncrementStrength = 1,
                    },
                    // Slots now live on the component itself, not the body extension.
                    Slots =
                    [
                        new UnitComponentSlotDto
                        {
                            DisplayName = "Weapon Slot",
                            IsVisible = true,
                            UIXPosition = 1,
                            UIYPosition = 0,
                            Rules =
                            [
                                new UnitComponentSlotRuleDto
                                {
                                    RuleType = SlotRuleType.ComponentType,
                                    Operator = SlotRuleOperator.Equals,
                                    Value = nameof(ComponentType.Weapon),
                                },
                            ],
                        },
                    ],
                },
                BuildWeapon(WEAPON_CODE),
                new UnitComponentDto
                {
                    Code = ARMOUR_CODE,
                    DisplayName = "Test Armour",
                    ComponentType = ComponentType.Armour,
                    Weight = 30,
                    Armour = new UnitComponentArmourDto
                    {
                        Resistance = new DamageProfileDto { Slash = 500, Pierce = 250 },
                    },
                },
            ],
        };
    }

    /// <summary>
    /// A standalone weapon component with the given code, a fixed attack profile and one Land modifier.
    /// </summary>
    private static UnitComponentDto BuildWeapon(string code)
    {
        return new UnitComponentDto
        {
            Code = code,
            DisplayName = "Test Weapon",
            ComponentType = ComponentType.Weapon,
            Weight = 10,
            Detection = 2000,
            Visibility = 3000,
            Weapon = new UnitComponentWeaponDto
            {
                Attack = new DamageProfileDto { Slash = 1000, Pierce = 500 },
                MinimumRange = 1,
                MaximumRange = 4,
                DomainModifiers =
                [
                    new UnitComponentWeaponDomainModifierDto { Domain = DomainType.Land, DamageMultiplier = 1000 },
                ],
            },
        };
    }
}
