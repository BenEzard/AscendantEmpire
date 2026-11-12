using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;
using AscendantEmpire.Tests.UnitDesigns.Shared;

using static AscendantEmpire.Tests.UnitDesigns.Shared.UnitDesignPayloads;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Exercises <see cref="BulkUpsertUnitComponentsHandler"/> over the component graph itself: what an insert
/// persists, what a re-import updates in place, and the type, tag and supersession rules that reject a
/// payload outright.
/// </summary>
/// <remarks>
/// <para>
/// Slot identity across a re-import is the other half of this handler's behaviour and lives in
/// <see cref="BulkUpsertSlotReconciliationTests"/>. The seam is real rather than filing convenience: these
/// tests never fit a component into a template, so nothing here turns on the fitted-row foreign key.
/// </para>
/// <para>
/// Tier 3 (containerised SQL Server) because these tests read back the <c>DamageProfile</c> complex type on
/// weapons and armour, which the in-memory provider throws on. The 1:1 shared-PK extensions this fixture
/// also uses are <em>not</em> a reason to be here — those work fine in memory. See <c>docs/testing.md</c>.
/// </para>
/// </remarks>
internal class BulkUpsertComponentGraphTests : SqlServerTestBase
{
    // The body and weapon codes are aliased from the shared payloads rather than referenced through them at
    // every call site, which would add noise without adding meaning. The armour and tag codes stay local:
    // only BuildFullCatalogue below uses them, so there is nothing to share.
    private const string BODY_CODE = UnitDesignPayloads.BODY_CODE;
    private const string WEAPON_CODE = UnitDesignPayloads.WEAPON_CODE;
    private const string ARMOUR_CODE = "test_armour";
    private const string TAG_CODE = "amphibious";
    private const string _HUMAN_FOOD = "human-food";
    private const string _ARROWS = "Weapons.Ammunition.Arrows";

    [Test, Description("Inserting a full component graph persists the parent, its 1:1 extension, child rows and tag joins.")]
    public async Task FullGraphInsertPersistsEntireGraphAsync()
    {
        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();

        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(BuildFullCatalogue()), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        UnitComponent body = await verifyContext.UnitComponents
            .Include(c => c.Body!).ThenInclude(b => b.Slots).ThenInclude(s => s.Rules)
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
            Assert.That(result.SuccessValue.UpdatedCount, Is.Zero, "Nothing should be updated.");

            // --- Body extension shares the parent PK; slots (now on the component) + rules ---
            Assert.That(body.Body, Is.Not.Null);
            Assert.That(body.Body!.UnitComponentId, Is.EqualTo(body.Id), "Body extension shares the component PK.");
            Assert.That(body.Body.MovementDomain, Is.EqualTo(DomainType.Land));
            Assert.That(body.Body.Slots, Has.Count.EqualTo(1));
            Assert.That(body.Body.Slots.Single().Rules, Has.Count.EqualTo(1));
            Assert.That(body.Body.Slots.Single().Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Weapon)));

            // --- Tag join ---
            Assert.That(body.Tags, Has.Count.EqualTo(1), "Body should carry one tag join.");

            // --- Main-table Sight/Detection/Visibility round-trip ---
            Assert.That(weapon.Sight, Is.EqualTo(500), "Sight should round-trip on insert.");
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
        BulkUpsertUnitComponentsHandler firstHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await firstHandler.Handle(new BulkUpsertUnitComponentsRequest(BuildFullCatalogue()), CancellationToken.None);

        // Capture the original surrogate ids so we can prove the rows were updated, not replaced.
        await using (AscendantEmpireContext beforeContext = await DbFactory.CreateDbContextAsync())
        {
            int originalWeaponId = await beforeContext.UnitComponents.Where(c => c.Code == WEAPON_CODE).Select(c => c.Id).SingleAsync();
            Assert.That(originalWeaponId, Is.GreaterThan(0));
        }

        // Build a mutated catalogue: rename the weapon, change its attack, and swap the body's slot.
        BulkUpsertUnitComponentsDto changed = BuildFullCatalogue();
        UnitComponentDto changedWeapon = changed.Components.Single(c => c.Code == WEAPON_CODE) with
        {
            DisplayName = "Upgraded Weapon",
            Sight = 4000,
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

        var changedCatalogue = new BulkUpsertUnitComponentsDto
        {
            Tags = changed.Tags,
            Components = [.. changed.Components.Select(c => c.Code == WEAPON_CODE ? changedWeapon : c)],
        };

        // Second run upserts the mutated payload.
        BulkUpsertUnitComponentsHandler secondHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await secondHandler.Handle(new BulkUpsertUnitComponentsRequest(changedCatalogue), CancellationToken.None);

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
            Assert.That(result.SuccessValue!.AddedCount, Is.Zero, "Nothing new on the second run.");
            Assert.That(result.SuccessValue.UpdatedCount, Is.EqualTo(3), "All three are updated.");
            Assert.That(result.SuccessValue.UpdatedCodes, Is.EquivalentTo([BODY_CODE, WEAPON_CODE, ARMOUR_CODE]));

            // --- No duplicate rows written. ---
            Assert.That(componentCount, Is.EqualTo(3), "No duplicate components written.");
            Assert.That(tagCount, Is.EqualTo(1), "No duplicate tag written.");

            // --- The weapon's scalars and children reflect the new payload. ---
            Assert.That(weapon.DisplayName, Is.EqualTo("Upgraded Weapon"), "Scalar field should be overwritten.");
            Assert.That(weapon.Sight, Is.EqualTo(4000), "Sight should be overwritten on update.");
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
        BulkUpsertUnitComponentsHandler seedHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await seedHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [BuildWeapon(WEAPON_CODE)] }),
            CancellationToken.None);

        // Re-submit the same weapon (renamed) plus a brand-new one.
        UnitComponentDto renamedWeapon = BuildWeapon(WEAPON_CODE) with { DisplayName = "Renamed Weapon" };

        var mixedCatalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [renamedWeapon, BuildWeapon("new_weapon")],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(mixedCatalogue), CancellationToken.None);

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
        BulkUpsertUnitComponentsHandler seedHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
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

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
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

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
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

    [Test, Description("A Body may also carry inherent Weapon and Armour data; all three extensions persist.")]
    public async Task BodyWithInherentWeaponAndArmourPersistsAsync()
    {
        var bodyWithCombat = new UnitComponentDto
        {
            Code = "armed_body",
            DisplayName = "Armed Body",
            ComponentType = ComponentType.Body,
            Body = new UnitComponentBodyDto
            {
                MovementDomain = DomainType.Land,
                // Raw fixed-point (Scale 1,000): 2,000 = 2.0 campaign, 4,000 = 4.0 battlefield movement points.
                CampaignMovementPoints = 2000,
                BattlefieldMovementPoints = 4000,
            },
            // Inherent attack/defence the body itself contributes.
            Weapon = new UnitComponentWeaponDto { Attack = new DamageProfileDto { Crush = 300 }, MinimumRange = 0, MaximumRange = 1 },
            Armour = new UnitComponentArmourDto { Resistance = new DamageProfileDto { Slash = 150 } },
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(
                new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [bodyWithCombat] }),
                CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        UnitComponent persisted = await verifyContext.UnitComponents
            .Include(c => c.Body)
            .Include(c => c.Weapon)
            .Include(c => c.Armour)
            .SingleAsync(c => c.Code == "armed_body");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "A body with inherent weapon/armour should be accepted.");
            Assert.That(persisted.Body, Is.Not.Null, "Body extension persists.");
            Assert.That(persisted.Weapon, Is.Not.Null, "Inherent weapon extension persists.");
            Assert.That(persisted.Weapon!.Attack.Crush, Is.EqualTo(300), "Body weapon attack round-trips.");
            Assert.That(persisted.Armour, Is.Not.Null, "Inherent armour extension persists.");
            Assert.That(persisted.Armour!.Resistance.Slash, Is.EqualTo(150), "Body armour resistance round-trips.");
        }
    }

    [Test, Description("Two components sharing a Code fail validation and write nothing.")]
    public async Task DuplicateCodeInPayloadFailsAsync()
    {
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("dup"), BuildWeapon("dup")],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

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

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
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
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword_basic"), BuildWeapon("sword_advanced")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_advanced", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

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
        var firstCatalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword_basic"), BuildWeapon("sword_adv1"), BuildWeapon("sword_adv2")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_adv1" },
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_adv2", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler firstHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await firstHandler.Handle(new BulkUpsertUnitComponentsRequest(firstCatalogue), CancellationToken.None);

        // Second import re-submits only the predecessor and a single link to adv1 (now recommended).
        // adv1 resolves from the database even though it isn't in this payload's Components list.
        var secondCatalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword_basic")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword_basic", SuccessorCode = "sword_adv1", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler secondHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await secondHandler.Handle(new BulkUpsertUnitComponentsRequest(secondCatalogue), CancellationToken.None);

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
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword", SuccessorCode = "sword" },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

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
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("sword")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "sword", SuccessorCode = "ghost" },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

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
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("a"), BuildWeapon("b")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "b" },
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "b" },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

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
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Components = [BuildWeapon("a"), BuildWeapon("b"), BuildWeapon("c")],
            ComponentSupersessions =
            [
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "b", IsRecommended = true },
                new ComponentSupersessionDto { PredecessorCode = "a", SuccessorCode = "c", IsRecommended = true },
            ],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result =
            await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

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
        var firstCatalogue = new BulkUpsertUnitComponentsDto
        {
            Tags = [new TagDto { Code = "sword", DisplayName = "Sword", TagType = TagType.Classification }],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler firstHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await firstHandler.Handle(new BulkUpsertUnitComponentsRequest(firstCatalogue), CancellationToken.None);

        await using (AscendantEmpireContext afterCreate = await DbFactory.CreateDbContextAsync())
        {
            TagType created = await afterCreate.Tags.Where(t => t.Code == "sword").Select(t => t.TagType).SingleAsync();
            Assert.That(created, Is.EqualTo(TagType.Classification), "TagType should be set on creation.");
        }

        // Re-import the same tag code re-classified as a Buff.
        var secondCatalogue = new BulkUpsertUnitComponentsDto
        {
            Tags = [new TagDto { Code = "sword", DisplayName = "Sword", TagType = TagType.Combat }],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler secondHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        await secondHandler.Handle(new BulkUpsertUnitComponentsRequest(secondCatalogue), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int tagCount = await verifyContext.Tags.CountAsync(t => t.Code == "sword");
        TagType updated = await verifyContext.Tags.Where(t => t.Code == "sword").Select(t => t.TagType).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tagCount, Is.EqualTo(1), "No duplicate tag should be created on re-import.");
            Assert.That(updated, Is.EqualTo(TagType.Combat), "TagType should be overwritten on re-import.");
        }
    }

    /// <summary>
    /// Builds an unled organisation tag DTO with the design doc's Settler sizing.
    /// </summary>
    private static TagDto BuildUnledSettlerTagDto()
    {
        return new TagDto
        {
            Code = OrganisationTagCodes.SETTLER,
            DisplayName = "Settler",
            TagType = TagType.MilitaryOrganisationalGroup,
            MovementDomain = DomainType.Land,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = 25,
            UnledDefaultSize = 200,
            UnledMaxSize = 5000,
        };
    }

    [Test, Description("An exported organisation tag round-trips through the catalogue: importing the projected DTO preserves the domain, leadership model and unled sizes.")]
    public async Task OrganisationTagRoundTripsThroughCatalogueAsync()
    {
        // Import the hand-authored tag, then re-export it exactly as the catalogue zip would.
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Tags = [BuildUnledSettlerTagDto()],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> imported = await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);
        Assert.That(imported.HasSucceeded, Is.True, () => string.Join("; ", imported.Errors));

        TagDto exported;

        await using (AscendantEmpireContext exportContext = await DbFactory.CreateDbContextAsync())
        {
            Tag persisted = await exportContext.Tags.SingleAsync(t => t.Code == OrganisationTagCodes.SETTLER);
            exported = CatalogueExportProjections.ToTagDto(persisted);
        }

        // Re-importing the exported DTO is the move-between-databases path; the organisation data must survive it.
        var reimportCatalogue = new BulkUpsertUnitComponentsDto
        {
            Tags = [exported],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler reimportHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> reimported = await reimportHandler.Handle(
            new BulkUpsertUnitComponentsRequest(reimportCatalogue), CancellationToken.None);
        Assert.That(reimported.HasSucceeded, Is.True, () => string.Join("; ", reimported.Errors));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        Tag roundTripped = await verifyContext.Tags.SingleAsync(t => t.Code == OrganisationTagCodes.SETTLER);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exported.LeadershipModel, Is.EqualTo(LeadershipModel.Unled), "The export projection carries the leadership model.");
            Assert.That(exported.UnledBaseSize, Is.EqualTo(25), "The export projection carries the unled sizes.");
            Assert.That(roundTripped.MovementDomain, Is.EqualTo(DomainType.Land));
            Assert.That(roundTripped.LeadershipModel, Is.EqualTo(LeadershipModel.Unled), "A settler tag must not arrive silently commissioned.");
            Assert.That(roundTripped.UnledBaseSize, Is.EqualTo(25));
            Assert.That(roundTripped.UnledDefaultSize, Is.EqualTo(200));
            Assert.That(roundTripped.UnledMaxSize, Is.EqualTo(5000));
        }
    }

    [Test, Description("The import applies the same unled sizing rules as the Tag manager — an incoherent tag is rejected and nothing is written.")]
    public async Task ImportRejectsIncoherentUnledSizingAsync()
    {
        // 90 is not a whole multiple of the 25 quantum, which the single-tag path also rejects.
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Tags = [BuildUnledSettlerTagDto() with { UnledDefaultSize = 90 }],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("whole multiple"));
            Assert.That(await verifyContext.Tags.AnyAsync(t => t.Code == OrganisationTagCodes.SETTLER), Is.False, "A rejected import writes nothing.");
        }
    }

    [Test, Description("The import rejects unled sizes on a tag that is not an organisation group, matching the single-tag path.")]
    public async Task ImportRejectsUnledSizesOnNonOrganisationTagAsync()
    {
        var catalogue = new BulkUpsertUnitComponentsDto
        {
            Tags = [BuildUnledSettlerTagDto() with { Code = "sneaky", TagType = TagType.Ability }],
            Components = [BuildWeapon(WEAPON_CODE)],
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(new BulkUpsertUnitComponentsRequest(catalogue), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Military Organisational Group"));
        }
    }
    // --- Payload builders -------------------------------------------------------------------

    /// <summary>
    /// A catalogue with one of each extension type plus a tag referenced by the body.
    /// </summary>
    private static BulkUpsertUnitComponentsDto BuildFullCatalogue()
    {
        return new BulkUpsertUnitComponentsDto
        {
            Tags = [new TagDto { Code = TAG_CODE, DisplayName = "Amphibious", TagType = TagType.Classification }],
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
                        // Raw fixed-point (Scale 1,000): 3,000 = 3.0 campaign, 6,000 = 6.0 battlefield movement points.
                        CampaignMovementPoints = 3000,
                        BattlefieldMovementPoints = 6000,
                        // Only bodies expose slots, so they live on the body extension.
                        Slots =
                        [
                            new UnitComponentSlotDto
                            {
                                Code = "weapon-1",
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

    #region Supply requirements

    [Test, Description("A supply requirement survives an export and re-import, which is the only way a catalogue moves between databases.")]
    public async Task SupplyRequirementRoundTripsThroughCatalogueAsync()
    {
        await SeedSupplySetAsync(_HUMAN_FOOD);

        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [new UnitComponentSupplyRequirementDto { SupplySetCode = _HUMAN_FOOD, Quantity = 1_500 }]
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> imported = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [body] }), CancellationToken.None);

        Assert.That(imported.HasSucceeded, Is.True, () => string.Join("; ", imported.Errors));

        UnitComponentDto exported;

        await using (AscendantEmpireContext exportContext = await DbFactory.CreateDbContextAsync())
        {
            UnitComponent persisted = await UnitComponentReconciler
                .WithFullGraph(exportContext.UnitComponents.Where(c => c.Code == BODY_CODE))
                .SingleAsync();

            exported = CatalogueExportProjections.ToUnitComponentDto(persisted);
        }

        // Re-importing the exported DTO is the move-between-databases path; the requirement must survive it rather than being reconciled away.
        BulkUpsertUnitComponentsHandler reimportHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> reimported = await reimportHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [exported] }), CancellationToken.None);

        Assert.That(reimported.HasSucceeded, Is.True, () => string.Join("; ", reimported.Errors));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        List<UnitComponentSupplyRequirement> roundTripped = await verifyContext.UnitComponentSupplyRequirements
            .Include(requirement => requirement.SupplySet)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exported.SupplyRequirements.Single().SupplySetCode, Is.EqualTo(_HUMAN_FOOD), "The export projection carries the set's code.");
            Assert.That(exported.SupplyRequirements.Single().Quantity, Is.EqualTo(1_500), "It carries the quantity.");
            Assert.That(roundTripped, Has.Count.EqualTo(1), "The re-import neither drops nor doubles the row.");
            Assert.That(roundTripped.Single().Quantity, Is.EqualTo(1_500));
        }
    }

    [Test, Description("A catalogue naming a supply set the importing database lacks is refused by name, rather than silently importing a component with no needs.")]
    public async Task UnknownSupplySetFailsTheImportAsync()
    {
        UnitComponentDto body = BuildBodyWithSlots() with
        {
            SupplyRequirements = [new UnitComponentSupplyRequirementDto { SupplySetCode = "NoSuchSet", Quantity = 1_000 }]
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> result = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [body] }), CancellationToken.None);

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("NoSuchSet"));
            Assert.That(await verifyContext.UnitComponents.AnyAsync(), Is.False, "A rejected catalogue writes nothing.");
        }
    }

    /// <summary>
    /// Inserts a memberless food set. What a set holds is the supply-set endpoints' concern; all a requirement needs is the code to resolve.
    /// </summary>
    private async Task SeedSupplySetAsync(string code)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.SupplySets.Add(new SupplySet { Code = code, DisplayName = code, SupplyType = SupplyType.Food });

        await context.SaveChangesAsync();
    }

    #endregion

    #region Weapon ammunition

    [Test, Description("A weapon's ammunition options survive an export and re-import, which is the only way a catalogue moves between databases.")]
    public async Task WeaponAmmunitionRoundTripsThroughCatalogueAsync()
    {
        await SeedAmmunitionAssetAsync(_ARROWS);

        UnitComponentDto weapon = BuildWeapon(WEAPON_CODE);
        weapon = weapon with
        {
            Weapon = weapon.Weapon! with
            {
                Ammunition = [new UnitComponentWeaponAmmunitionDto { AssetCode = _ARROWS }]
            }
        };

        BulkUpsertUnitComponentsHandler handler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> imported = await handler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [weapon] }), CancellationToken.None);

        Assert.That(imported.HasSucceeded, Is.True, () => string.Join("; ", imported.Errors));

        UnitComponentDto exported;

        await using (AscendantEmpireContext exportContext = await DbFactory.CreateDbContextAsync())
        {
            UnitComponent persisted = await UnitComponentReconciler
                .WithFullGraph(exportContext.UnitComponents.Where(c => c.Code == WEAPON_CODE))
                .SingleAsync();

            exported = CatalogueExportProjections.ToUnitComponentDto(persisted);
        }

        BulkUpsertUnitComponentsHandler reimportHandler = await DbFactory.CreateBulkUpsertHandlerAsync();
        ServiceResult<BulkUpsertUnitComponentsResult> reimported = await reimportHandler.Handle(
            new BulkUpsertUnitComponentsRequest(new BulkUpsertUnitComponentsDto { Components = [exported] }), CancellationToken.None);

        Assert.That(reimported.HasSucceeded, Is.True, () => string.Join("; ", reimported.Errors));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        List<UnitComponentWeaponAmmunition> roundTripped = await verifyContext.UnitComponentWeaponAmmunitions
            .Include(option => option.Asset)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exported.Weapon!.Ammunition.Single().AssetCode, Is.EqualTo(_ARROWS), "The export projection carries the Asset's code.");
            Assert.That(roundTripped, Has.Count.EqualTo(1), "The re-import neither drops nor doubles the row.");
            Assert.That(roundTripped.Single().Asset.Code, Is.EqualTo(_ARROWS));
        }
    }

    /// <summary>
    /// Inserts one ammunition Asset, with the meta-category and storage class an Asset row cannot do without.
    /// </summary>
    private async Task SeedAmmunitionAssetAsync(string code)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var metaCategory = new AssetMetaCategory { Code = "Weapons", DisplayName = "Weapons", SortOrder = 1, IconPath = "/resources/none.svg" };
        var storageClass = new StorageClass { Code = "TestArmoury", DisplayName = "Test Armoury" };

        context.AssetMetaCategories.Add(metaCategory);
        context.StorageClasses.Add(storageClass);
        await context.SaveChangesAsync();

        // An Asset hangs off exactly one of a category or a group, which CK_Asset_GroupOrCategoryXor enforces.
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

        context.Assets.Add(new Asset
        {
            Code = code,
            DisplayName = code.Split('.').Last(),
            IconPath = "/resources/none.svg",
            IsGradedAsset = false,
            SortOrder = 0,
            AssetMetaCategoryId = metaCategory.Id,
            AssetCategoryId = category.Id,
            StorageClassId = storageClass.Id,
        });

        await context.SaveChangesAsync();
    }

    #endregion
}
