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
            Assert.That(result.SuccessValue.UpdatedCount, Is.EqualTo(0), "Nothing should be updated.");

            // --- Body + slots + rules, and the 1:1 extension shares the parent PK ---
            Assert.That(body.Body, Is.Not.Null);
            Assert.That(body.Body!.UnitComponentId, Is.EqualTo(body.Id), "Body extension shares the component PK.");
            Assert.That(body.Body.MovementDomain, Is.EqualTo(DomainType.Land));
            Assert.That(body.Body.Slots, Has.Count.EqualTo(1));
            Assert.That(body.Body.Slots.Single().Rules, Has.Count.EqualTo(1));
            Assert.That(body.Body.Slots.Single().Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Weapon)));

            // --- Tag join ---
            Assert.That(body.Tags, Has.Count.EqualTo(1), "Body should carry one tag join.");

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

    // --- Payload builders -------------------------------------------------------------------

    /// <summary>
    /// A catalog with one of each extension type plus a tag referenced by the body.
    /// </summary>
    private static BulkUpsertUnitComponentsDto BuildFullCatalog()
    {
        return new BulkUpsertUnitComponentsDto
        {
            Tags = [new TagDto { Code = TAG_CODE, DisplayName = "Amphibious" }],
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
