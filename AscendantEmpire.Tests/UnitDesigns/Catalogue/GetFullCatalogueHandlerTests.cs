using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalogue;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Covers the dev god-view read: it returns the whole catalogue (bodies, components, tags, templates) with no
/// game/player or unlock filtering, and every leaf of that projection carries the value it was seeded with.
/// </summary>
/// <remarks>
/// <para>
/// Tier 3 despite being a read-only test: the components half of the projection dereferences the
/// <c>DamageProfile</c> complex type, which the in-memory provider throws on. The bodies half would run in
/// memory quite happily, but splitting one endpoint's suite across two providers buys nothing.
/// See <c>docs/testing.md</c>.
/// </para>
/// <para>
/// Every seeded value is distinct from every other value of the same type, so a projection that reads the
/// right column into the wrong property fails rather than passing by coincidence. That is the whole reason
/// the movement points are not all 1,000 and the damage channels are not all equal.
/// </para>
/// <para>
/// Those values live in the <c>Seeded*</c> holders below rather than as literals, because the seed and the
/// assertion that checks it sit a couple of hundred lines apart: a literal at the assertion site says nothing
/// about where it came from, and editing the seed would leave it silently stale. Naming them keeps the
/// assertions non-tautological — each holder is a plain constant, not a value derived by the code under test.
/// </para>
/// <para>
/// <b>Nothing in this projection is ordered</b> — not the four top-level collections, not a body's slots, not
/// a template's fitted components. Assertions therefore look rows up by a distinguishing value rather than by
/// position; asserting an index would be asserting an implementation detail the handler never promised.
/// </para>
/// </remarks>
internal class GetFullCatalogueHandlerTests : SqlServerTestBase
{
    // One holder per seeded row. Both the seed and the assertions read their values from here, so a value can
    // never be changed in one place and left stale in the other, and the distinctness rule described in the
    // class remark is checkable by reading down the file rather than by hunting literals through the tests.
    // Booleans stay as literals: IsVisible and DualWield are asserted with Is.True/Is.False, which says more
    // at the assertion site than a named constant would.

    /// <summary>
    /// The body under test. Its weight, detection, visibility and four body stats are raw fixed-point at
    /// Scale 1,000, so 3,000 campaign movement points is 3.0 and 20,000 hitpoints is 20.0.
    /// </summary>
    private static class SeededBody
    {
        public const string CODE = "test_body";
        public const string DISPLAY_NAME = "Test Body";
        public const string DESCRIPTION = "The body under test.";
        public const string DESIGNER_IMAGE_PATH = "body.png";
        public const long WEIGHT = 100;
        public const long SIGHT = 1100;
        public const long DETECTION = 2100;
        public const long VISIBILITY = 3100;

        /// <summary>Deliberately not the default Land, so a projection hard-coding the default would fail.</summary>
        public const DomainType MOVEMENT_DOMAIN = DomainType.Water;

        public const long CAMPAIGN_MOVEMENT_POINTS = 3000;
        public const long BATTLEFIELD_MOVEMENT_POINTS = 6000;
        public const long POWER = 1500;
        public const long HITPOINTS = 20000;
    }

    /// <summary>The body's visible slot: two acceptance rules, and the slot the global template fits a weapon into.</summary>
    private static class SeededWeaponSlot
    {
        public const string CODE = "weapon-1";
        public const string DISPLAY_NAME = "Weapon 1";
        public const decimal UI_X_POSITION = -0.25m;
        public const decimal UI_Y_POSITION = 0.5m;
    }

    /// <summary>The body's hidden slot: one rule and mirrored positions, so neither slot can stand in for the other.</summary>
    private static class SeededArmourSlot
    {
        public const string CODE = "armour-1";
        public const string DISPLAY_NAME = "Armour 1";
        public const decimal UI_X_POSITION = 0.75m;
        public const decimal UI_Y_POSITION = -0.125m;
    }

    /// <summary>
    /// The weapon under test. Its five attack channels differ from each other and from the armour's five
    /// resistances, so a projection that crosses two channels cannot pass by coincidence.
    /// </summary>
    private static class SeededWeapon
    {
        public const string CODE = "test_weapon";
        public const string DISPLAY_NAME = "Test Weapon";
        public const string DESCRIPTION = "The weapon under test.";
        public const long WEIGHT = 10;
        public const long SIGHT = 1200;
        public const long DETECTION = 2200;
        public const long VISIBILITY = 3200;
        public const long ATTACK_SLASH = 1000;
        public const long ATTACK_PIERCE = 500;
        public const long ATTACK_CRUSH = 250;
        public const long ATTACK_BURN = 125;
        public const long ATTACK_POISON = 60;
        public const int MINIMUM_RANGE = 2;
        public const int MAXIMUM_RANGE = 5;
        public const long LAND_DAMAGE_MULTIPLIER = 1000;
        public const long AIR_DAMAGE_MULTIPLIER = 400;
    }

    /// <summary>The armour under test: the extension the old smoke test never seeded at all.</summary>
    private static class SeededArmour
    {
        public const string CODE = "test_armour";
        public const string DISPLAY_NAME = "Test Armour";
        public const long WEIGHT = 50;
        public const long RESISTANCE_SLASH = 11;
        public const long RESISTANCE_PIERCE = 22;
        public const long RESISTANCE_CRUSH = 33;
        public const long RESISTANCE_BURN = 44;
        public const long RESISTANCE_POISON = 55;
    }

    /// <summary>A Body-typed component deliberately left without its 1:1 body extension.</summary>
    private static class SeededExtensionlessBody
    {
        public const string CODE = "test_body_no_extension";
        public const string DISPLAY_NAME = "Body Without Extension";
        public const long WEIGHT = 1;
    }

    /// <summary>The tag attached to the body: system-created, and the only one carrying a description.</summary>
    private static class SeededCombatTag
    {
        public const string CODE = "amphibious";
        public const string DISPLAY_NAME = "Amphibious";
        public const string DESCRIPTION = "Can cross water without a bridge.";
    }

    /// <summary>The tag attached to the weapon: not system-created, and deliberately left without a description.</summary>
    private static class SeededAbilityTag
    {
        public const string CODE = "elite";
        public const string DISPLAY_NAME = "Elite";
    }

    /// <summary>The pre-defined template: coded, unowned, and the only one with a component fitted.</summary>
    private static class SeededGlobalTemplate
    {
        public const string CODE = "tmpl_global";
        public const string DISPLAY_NAME = "Global Template";
        public const string DESCRIPTION = "A pre-defined template.";
        public const string MAP_IMAGE_PATH = "global.png";
    }

    /// <summary>The player-owned template: uncoded, with nothing fitted into it.</summary>
    private static class SeededCustomTemplate
    {
        public const string DISPLAY_NAME = "Custom Template";
        public const string MAP_IMAGE_PATH = "custom.png";
    }

    /// <summary>
    /// The database ids the seed generated, for the assertions that need to match a projected id back to the
    /// row it came from.
    /// </summary>
    private sealed record SeededCatalogue(
        int BodyId,
        int WeaponId,
        int WeaponSlotId,
        int ArmourSlotId,
        int GlobalTemplateId,
        int CustomTemplateId,
        int FittedComponentId);

    /// <summary>
    /// Seeds one of everything the four projections can reach, so no branch is left un-evaluated.
    /// </summary>
    /// <remarks>
    /// Deliberately seeds entities directly rather than through the write handlers: this is a read test, and
    /// routing it through the create endpoint would make a write-path regression look like a read-path bug.
    /// </remarks>
    private async Task<SeededCatalogue> SeedRichCatalogueAsync()
    {
        await using AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync();

        // --- Tags -------------------------------------------------------------------------------------
        // Two tags with opposite SystemCreated values and only one carrying a Description, so both states of
        // each are exercised. Only the first is attached to a component; the second proves the Tags
        // collection is the whole tag table rather than "tags in use".
        var combatTag = new Tag
        {
            Code = SeededCombatTag.CODE,
            DisplayName = SeededCombatTag.DISPLAY_NAME,
            Description = SeededCombatTag.DESCRIPTION,
            TagType = TagType.Combat,
            SystemCreated = true,
        };

        var abilityTag = new Tag
        {
            Code = SeededAbilityTag.CODE,
            DisplayName = SeededAbilityTag.DISPLAY_NAME,
            TagType = TagType.Ability,
            SystemCreated = false,
        };

        // --- Body, with two slots carrying differing rule counts ----------------------------------------
        var weaponSlot = new UnitComponentSlot
        {
            Code = SeededWeaponSlot.CODE,
            DisplayName = SeededWeaponSlot.DISPLAY_NAME,
            IsVisible = true,
            UIXPosition = SeededWeaponSlot.UI_X_POSITION,
            UIYPosition = SeededWeaponSlot.UI_Y_POSITION,
            Rules =
            [
                new UnitComponentSlotRule { RuleType = SlotRuleType.ComponentType, Operator = SlotRuleOperator.Equals, Value = nameof(ComponentType.Weapon) },
                new UnitComponentSlotRule { RuleType = SlotRuleType.HasTag, Operator = SlotRuleOperator.NotEquals, Value = SeededAbilityTag.CODE },
            ],
        };

        // IsVisible false and a single rule, so neither slot can stand in for the other.
        var armourSlot = new UnitComponentSlot
        {
            Code = SeededArmourSlot.CODE,
            DisplayName = SeededArmourSlot.DISPLAY_NAME,
            IsVisible = false,
            UIXPosition = SeededArmourSlot.UI_X_POSITION,
            UIYPosition = SeededArmourSlot.UI_Y_POSITION,
            Rules =
            [
                new UnitComponentSlotRule { RuleType = SlotRuleType.ComponentType, Operator = SlotRuleOperator.Equals, Value = nameof(ComponentType.Armour) },
            ],
        };

        var body = new UnitComponent
        {
            Code = SeededBody.CODE,
            DisplayName = SeededBody.DISPLAY_NAME,
            Description = SeededBody.DESCRIPTION,
            DesignerImagePath = SeededBody.DESIGNER_IMAGE_PATH,
            ComponentType = ComponentType.Body,
            Weight = SeededBody.WEIGHT,
            Sight = SeededBody.SIGHT,
            Detection = SeededBody.DETECTION,
            Visibility = SeededBody.VISIBILITY,
            Tags = [new UnitComponentTag { Tag = combatTag }],
            Body = new UnitComponentBody
            {
                MovementDomain = SeededBody.MOVEMENT_DOMAIN,
                // Four different values so a swapped pair is visible. See SeededBody for the fixed-point scale.
                CampaignMovementPoints = SeededBody.CAMPAIGN_MOVEMENT_POINTS,
                BattlefieldMovementPoints = SeededBody.BATTLEFIELD_MOVEMENT_POINTS,
                Power = SeededBody.POWER,
                Hitpoints = SeededBody.HITPOINTS,
                Slots = [weaponSlot, armourSlot],
            },
        };

        // --- Weapon: all five damage channels distinct, two domain modifiers ----------------------------
        var weapon = new UnitComponent
        {
            Code = SeededWeapon.CODE,
            DisplayName = SeededWeapon.DISPLAY_NAME,
            Description = SeededWeapon.DESCRIPTION,
            ComponentType = ComponentType.Weapon,
            Weight = SeededWeapon.WEIGHT,
            Sight = SeededWeapon.SIGHT,
            Detection = SeededWeapon.DETECTION,
            Visibility = SeededWeapon.VISIBILITY,
            Tags = [new UnitComponentTag { Tag = abilityTag }],
            Weapon = new UnitComponentWeapon
            {
                Attack = new DamageProfile
                {
                    Slash = SeededWeapon.ATTACK_SLASH,
                    Pierce = SeededWeapon.ATTACK_PIERCE,
                    Crush = SeededWeapon.ATTACK_CRUSH,
                    Burn = SeededWeapon.ATTACK_BURN,
                    Poison = SeededWeapon.ATTACK_POISON,
                },
                MinimumRange = SeededWeapon.MINIMUM_RANGE,
                MaximumRange = SeededWeapon.MAXIMUM_RANGE,
                DualWield = true,
                DomainModifiers =
                [
                    new UnitComponentWeaponDomainModifier { Domain = DomainType.Land, DamageMultiplier = SeededWeapon.LAND_DAMAGE_MULTIPLIER },
                    new UnitComponentWeaponDomainModifier { Domain = DomainType.Air, DamageMultiplier = SeededWeapon.AIR_DAMAGE_MULTIPLIER },
                ],
            },
        };

        // --- Armour: the extension the old smoke test never seeded at all -------------------------------
        var armour = new UnitComponent
        {
            Code = SeededArmour.CODE,
            DisplayName = SeededArmour.DISPLAY_NAME,
            ComponentType = ComponentType.Armour,
            Weight = SeededArmour.WEIGHT,
            Armour = new UnitComponentArmour
            {
                Resistance = new DamageProfile
                {
                    Slash = SeededArmour.RESISTANCE_SLASH,
                    Pierce = SeededArmour.RESISTANCE_PIERCE,
                    Crush = SeededArmour.RESISTANCE_CRUSH,
                    Burn = SeededArmour.RESISTANCE_BURN,
                    Poison = SeededArmour.RESISTANCE_POISON,
                },
            },
        };

        // Body-typed but with no Body extension. The Bodies query requires both, and the Components query
        // excludes anything Body-typed, so this row must fall through both filters.
        var extensionlessBody = new UnitComponent
        {
            Code = SeededExtensionlessBody.CODE,
            DisplayName = SeededExtensionlessBody.DISPLAY_NAME,
            ComponentType = ComponentType.Body,
            Weight = SeededExtensionlessBody.WEIGHT,
        };

        seed.UnitComponents.AddRange(body, weapon, armour, extensionlessBody);
        seed.Tags.Add(abilityTag);
        await seed.SaveChangesAsync();

        // --- A game and player, so one template can be player-owned -------------------------------------
        // UnitTemplate.PlayerId is a real foreign key, so IsCustom == true cannot be exercised without a
        // Player row behind it. GameIndex.CampaignMapIndexId has no FK configured, so the chain stops here.
        var game = new GameIndex
        {
            GameName = "Catalogue Test Game",
            NumberOfPlayers = 1,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = TestGameSettings.MINIMUM_BATTLE_MAP_SIZE,
        };

        var player = new Player
        {
            Game = game,
            RulerTitle = "Emperor",
            RulerName = "Test Ruler",
            FactionName = "Test Faction",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/ruler_01.png",
            Status = PlayerStatus.Active,
        };

        seed.Add(player);
        await seed.SaveChangesAsync();

        // --- Templates: one global (coded, unowned), one custom (uncoded, player-owned) -----------------
        var globalTemplate = new UnitTemplate
        {
            Code = SeededGlobalTemplate.CODE,
            DisplayName = SeededGlobalTemplate.DISPLAY_NAME,
            Description = SeededGlobalTemplate.DESCRIPTION,
            MapImagePath = SeededGlobalTemplate.MAP_IMAGE_PATH,
            BodyComponentId = body.Id,
            // The fitted component: the only thing that makes DesignTemplateComponent appear at all.
            Components =
            [
                new UnitTemplateComponent { UnitComponentSlotId = weaponSlot.Id, UnitComponentId = weapon.Id },
            ],
        };

        var customTemplate = new UnitTemplate
        {
            DisplayName = SeededCustomTemplate.DISPLAY_NAME,
            MapImagePath = SeededCustomTemplate.MAP_IMAGE_PATH,
            GameId = game.Id,
            PlayerId = player.Id,
            BodyComponentId = body.Id,
        };

        seed.UnitTemplates.AddRange(globalTemplate, customTemplate);
        await seed.SaveChangesAsync();

        return new SeededCatalogue(
            BodyId: body.Id,
            WeaponId: weapon.Id,
            WeaponSlotId: weaponSlot.Id,
            ArmourSlotId: armourSlot.Id,
            GlobalTemplateId: globalTemplate.Id,
            CustomTemplateId: customTemplate.Id,
            FittedComponentId: globalTemplate.Components.Single().Id);
    }

    /// <summary>
    /// Runs the handler and unwraps a successful result, failing with the handler's own errors if it did not
    /// succeed rather than with a bare null-reference further down the test.
    /// </summary>
    private async Task<FullCatalogueResult> FetchCatalogueAsync()
    {
        var handler = new GetFullCatalogueHandler(DbFactory);
        ServiceResult<FullCatalogueResult> result = await handler.Handle(new GetFullCatalogueRequest(), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success), () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    [Test, Description("An empty database returns a success with four empty collections, not nulls or an error.")]
    public async Task EmptyCatalogueReturnsFourEmptyCollectionsAsync()
    {
        // Nothing is seeded. The dev tool loads this endpoint on a fresh database before anything has been
        // authored, so "no rows" has to be an ordinary success rather than a 500.
        FullCatalogueResult catalogue = await FetchCatalogueAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(catalogue.Bodies, Is.Empty);
            Assert.That(catalogue.Components, Is.Empty);
            Assert.That(catalogue.Tags, Is.Empty);
            Assert.That(catalogue.Templates, Is.Empty);
        }
    }

    [Test, Description("Every scalar on a projected body carries the value it was seeded with.")]
    public async Task BodyScalarsAreProjectedAsync()
    {
        SeededCatalogue seeded = await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();
        DesignBody projectedBody = catalogue.Bodies.Single(candidate => candidate.Code == SeededBody.CODE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projectedBody.Id, Is.EqualTo(seeded.BodyId));
            Assert.That(projectedBody.DisplayName, Is.EqualTo(SeededBody.DISPLAY_NAME));
            Assert.That(projectedBody.Description, Is.EqualTo(SeededBody.DESCRIPTION));
            Assert.That(projectedBody.Weight, Is.EqualTo(SeededBody.WEIGHT));
            Assert.That(projectedBody.Sight, Is.EqualTo(SeededBody.SIGHT));
            Assert.That(projectedBody.Detection, Is.EqualTo(SeededBody.DETECTION));
            Assert.That(projectedBody.Visibility, Is.EqualTo(SeededBody.VISIBILITY));
            Assert.That(projectedBody.MovementDomain, Is.EqualTo(SeededBody.MOVEMENT_DOMAIN), "Seeded as Water rather than the default Land, so a hard-coded default would fail.");
            Assert.That(projectedBody.CampaignMovementPoints, Is.EqualTo(SeededBody.CAMPAIGN_MOVEMENT_POINTS));
            Assert.That(projectedBody.BattlefieldMovementPoints, Is.EqualTo(SeededBody.BATTLEFIELD_MOVEMENT_POINTS));
            Assert.That(projectedBody.Power, Is.EqualTo(SeededBody.POWER));
            Assert.That(projectedBody.Hitpoints, Is.EqualTo(SeededBody.HITPOINTS));
            Assert.That(projectedBody.TagCodes, Is.EquivalentTo([SeededCombatTag.CODE]));
        }
    }

    [Test, Description("A body's slots and each slot's acceptance rules are projected in full.")]
    public async Task BodySlotsAndRulesAreProjectedAsync()
    {
        SeededCatalogue seeded = await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();
        DesignBody projectedBody = catalogue.Bodies.Single(candidate => candidate.Code == SeededBody.CODE);

        // DesignSlot carries no Code, so DisplayName is the only distinguishing value available here.
        DesignSlot weaponSlot = projectedBody.Slots.Single(slot => slot.DisplayName == SeededWeaponSlot.DISPLAY_NAME);
        DesignSlot armourSlot = projectedBody.Slots.Single(slot => slot.DisplayName == SeededArmourSlot.DISPLAY_NAME);

        DesignSlotRule componentTypeRule = weaponSlot.Rules.Single(rule => rule.RuleType == SlotRuleType.ComponentType);
        DesignSlotRule hasTagRule = weaponSlot.Rules.Single(rule => rule.RuleType == SlotRuleType.HasTag);

        using (Assert.EnterMultipleScope())
        {
            // Equivalence rather than a count: it pins down which rows came back, so a projection that returned one slot twice fails where a size check would happily pass.
            Assert.That(projectedBody.Slots.Select(slot => slot.DisplayName), Is.EquivalentTo([SeededWeaponSlot.DISPLAY_NAME, SeededArmourSlot.DISPLAY_NAME]));

            Assert.That(weaponSlot.Id, Is.EqualTo(seeded.WeaponSlotId), "The slot id is what a template's fitted components reference.");
            Assert.That(weaponSlot.IsVisible, Is.True);
            Assert.That(weaponSlot.UIXPosition, Is.EqualTo(SeededWeaponSlot.UI_X_POSITION));
            Assert.That(weaponSlot.UIYPosition, Is.EqualTo(SeededWeaponSlot.UI_Y_POSITION));

            Assert.That(armourSlot.Id, Is.EqualTo(seeded.ArmourSlotId));
            Assert.That(armourSlot.IsVisible, Is.False, "Both states of IsVisible are exercised.");
            Assert.That(armourSlot.UIXPosition, Is.EqualTo(SeededArmourSlot.UI_X_POSITION));
            Assert.That(armourSlot.UIYPosition, Is.EqualTo(SeededArmourSlot.UI_Y_POSITION));

            Assert.That(weaponSlot.Rules.Select(rule => rule.RuleType), Is.EquivalentTo([SlotRuleType.ComponentType, SlotRuleType.HasTag]));
            Assert.That(componentTypeRule.Operator, Is.EqualTo(SlotRuleOperator.Equals));
            Assert.That(componentTypeRule.Value, Is.EqualTo(nameof(ComponentType.Weapon)));
            Assert.That(hasTagRule.Operator, Is.EqualTo(SlotRuleOperator.NotEquals), "A non-default operator, so the enum is genuinely read.");
            Assert.That(hasTagRule.Value, Is.EqualTo(SeededAbilityTag.CODE));

            Assert.That(armourSlot.Rules.Single().Value, Is.EqualTo(nameof(ComponentType.Armour)), "Slots carry their own rules, not the sibling's.");
        }
    }

    [Test, Description("A weapon's stats, all five damage channels and its domain modifiers are projected.")]
    public async Task WeaponStatsAreProjectedAsync()
    {
        SeededCatalogue seeded = await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();
        DesignComponent projectedWeapon = catalogue.Components.Single(candidate => candidate.Code == SeededWeapon.CODE);

        DesignWeapon weapon = projectedWeapon.Weapon!;
        DesignWeaponDomainModifier landModifier = weapon.DomainModifiers.Single(modifier => modifier.Domain == DomainType.Land);
        DesignWeaponDomainModifier airModifier = weapon.DomainModifiers.Single(modifier => modifier.Domain == DomainType.Air);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projectedWeapon.Id, Is.EqualTo(seeded.WeaponId));
            Assert.That(projectedWeapon.DisplayName, Is.EqualTo(SeededWeapon.DISPLAY_NAME));
            Assert.That(projectedWeapon.Description, Is.EqualTo(SeededWeapon.DESCRIPTION));
            Assert.That(projectedWeapon.ComponentType, Is.EqualTo(ComponentType.Weapon));
            Assert.That(projectedWeapon.Weight, Is.EqualTo(SeededWeapon.WEIGHT));
            Assert.That(projectedWeapon.Sight, Is.EqualTo(SeededWeapon.SIGHT));
            Assert.That(projectedWeapon.Detection, Is.EqualTo(SeededWeapon.DETECTION));
            Assert.That(projectedWeapon.Visibility, Is.EqualTo(SeededWeapon.VISIBILITY));
            Assert.That(projectedWeapon.TagCodes, Is.EquivalentTo([SeededAbilityTag.CODE]));

            Assert.That(weapon.Attack.Slash, Is.EqualTo(SeededWeapon.ATTACK_SLASH));
            Assert.That(weapon.Attack.Pierce, Is.EqualTo(SeededWeapon.ATTACK_PIERCE));
            Assert.That(weapon.Attack.Crush, Is.EqualTo(SeededWeapon.ATTACK_CRUSH));
            Assert.That(weapon.Attack.Burn, Is.EqualTo(SeededWeapon.ATTACK_BURN));
            Assert.That(weapon.Attack.Poison, Is.EqualTo(SeededWeapon.ATTACK_POISON));

            Assert.That(weapon.MinimumRange, Is.EqualTo(SeededWeapon.MINIMUM_RANGE));
            Assert.That(weapon.MaximumRange, Is.EqualTo(SeededWeapon.MAXIMUM_RANGE));
            Assert.That(weapon.DualWield, Is.True);

            Assert.That(weapon.DomainModifiers.Select(modifier => modifier.Domain), Is.EquivalentTo([DomainType.Land, DomainType.Air]));
            Assert.That(landModifier.DamageMultiplier, Is.EqualTo(SeededWeapon.LAND_DAMAGE_MULTIPLIER));
            Assert.That(airModifier.DamageMultiplier, Is.EqualTo(SeededWeapon.AIR_DAMAGE_MULTIPLIER), "Each modifier keeps its own multiplier.");

            Assert.That(projectedWeapon.Armour, Is.Null, "A weapon has no armour extension.");
        }
    }

    [Test, Description("An armour's five resistance channels are projected.")]
    public async Task ArmourResistancesAreProjectedAsync()
    {
        await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();
        DesignComponent projectedArmour = catalogue.Components.Single(candidate => candidate.Code == SeededArmour.CODE);

        DesignDamageProfile resistance = projectedArmour.Armour!.Resistance;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projectedArmour.ComponentType, Is.EqualTo(ComponentType.Armour));
            Assert.That(resistance.Slash, Is.EqualTo(SeededArmour.RESISTANCE_SLASH));
            Assert.That(resistance.Pierce, Is.EqualTo(SeededArmour.RESISTANCE_PIERCE));
            Assert.That(resistance.Crush, Is.EqualTo(SeededArmour.RESISTANCE_CRUSH));
            Assert.That(resistance.Burn, Is.EqualTo(SeededArmour.RESISTANCE_BURN));
            Assert.That(resistance.Poison, Is.EqualTo(SeededArmour.RESISTANCE_POISON));

            Assert.That(projectedArmour.Weapon, Is.Null, "An armour has no weapon extension.");
            Assert.That(projectedArmour.Description, Is.Null, "An unset description projects as null rather than empty.");
            Assert.That(projectedArmour.Sight, Is.Null);
            Assert.That(projectedArmour.Detection, Is.Null);
            Assert.That(projectedArmour.Visibility, Is.Null);
            Assert.That(projectedArmour.TagCodes, Is.Empty, "An untagged component gets an empty collection, not null.");
        }
    }

    [Test, Description("Bodies and non-bodies are split cleanly, and a Body-typed row missing its extension falls into neither.")]
    public async Task ComponentTypeSplitPutsEachRowInExactlyOneCollectionAsync()
    {
        await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();

        IReadOnlyCollection<string> bodyCodes = [.. catalogue.Bodies.Select(body => body.Code)];
        IReadOnlyCollection<string> componentCodes = [.. catalogue.Components.Select(component => component.Code)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bodyCodes, Is.EquivalentTo([SeededBody.CODE]));
            Assert.That(componentCodes, Is.EquivalentTo([SeededWeapon.CODE, SeededArmour.CODE]));

            // The Bodies query needs ComponentType == Body *and* a body extension; the Components query excludes everything Body-typed.
            // A Body-typed row with no extension satisfies neither, so it is invisible to the dev tool rather than appearing twice or crashing the projection.
            Assert.That(bodyCodes, Does.Not.Contain(SeededExtensionlessBody.CODE));
            Assert.That(componentCodes, Does.Not.Contain(SeededExtensionlessBody.CODE));
        }
    }

    [Test, Description("Every tag in the table is returned with all five of its projected properties.")]
    public async Task TagsAreProjectedAsync()
    {
        await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();

        CatalogueTag combatTag = catalogue.Tags.Single(tag => tag.Code == SeededCombatTag.CODE);
        CatalogueTag abilityTag = catalogue.Tags.Single(tag => tag.Code == SeededAbilityTag.CODE);

        using (Assert.EnterMultipleScope())
        {
            // Both seeded tags and nothing else: the ability tag is attached to no component, so this is also
            // what proves the collection is the whole tag table rather than "tags in use".
            Assert.That(catalogue.Tags.Select(tag => tag.Code), Is.EquivalentTo([SeededCombatTag.CODE, SeededAbilityTag.CODE]));

            Assert.That(combatTag.Id, Is.GreaterThan(0));
            Assert.That(combatTag.DisplayName, Is.EqualTo(SeededCombatTag.DISPLAY_NAME));
            Assert.That(combatTag.Description, Is.EqualTo(SeededCombatTag.DESCRIPTION));
            Assert.That(combatTag.SystemCreated, Is.True);

            Assert.That(abilityTag.DisplayName, Is.EqualTo(SeededAbilityTag.DISPLAY_NAME));
            Assert.That(abilityTag.Description, Is.Null);
            Assert.That(abilityTag.SystemCreated, Is.False, "Both states of SystemCreated are exercised.");
        }
    }

    [Test, Description("Templates are projected with their denormalised body details, and IsCustom tracks player ownership.")]
    public async Task TemplatesAreProjectedAsync()
    {
        SeededCatalogue seeded = await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();

        DesignTemplate globalTemplate = catalogue.Templates.Single(template => template.Id == seeded.GlobalTemplateId);
        DesignTemplate customTemplate = catalogue.Templates.Single(template => template.Id == seeded.CustomTemplateId);

        using (Assert.EnterMultipleScope())
        {
            // The global and the custom template, and no third row: the god-view applies no player scoping,
            // but it must not duplicate a template either.
            Assert.That(catalogue.Templates.Select(template => template.Id), Is.EquivalentTo([seeded.GlobalTemplateId, seeded.CustomTemplateId]));

            Assert.That(globalTemplate.Code, Is.EqualTo(SeededGlobalTemplate.CODE));
            Assert.That(globalTemplate.DisplayName, Is.EqualTo(SeededGlobalTemplate.DISPLAY_NAME));
            Assert.That(globalTemplate.Description, Is.EqualTo(SeededGlobalTemplate.DESCRIPTION));
            Assert.That(globalTemplate.MapImagePath, Is.EqualTo(SeededGlobalTemplate.MAP_IMAGE_PATH));
            Assert.That(globalTemplate.IsCustom, Is.False, "No PlayerId means a pre-defined template.");

            Assert.That(customTemplate.Code, Is.Null, "Custom templates carry no code.");
            Assert.That(customTemplate.DisplayName, Is.EqualTo(SeededCustomTemplate.DISPLAY_NAME));
            Assert.That(customTemplate.IsCustom, Is.True, "A PlayerId means a player-owned template.");

            // Denormalised from the body component so the dev tool need not join the catalogue itself.
            Assert.That(globalTemplate.BodyComponentId, Is.EqualTo(seeded.BodyId));
            Assert.That(globalTemplate.BodyComponentCode, Is.EqualTo(SeededBody.CODE));
            Assert.That(globalTemplate.BodyComponentDisplayName, Is.EqualTo(SeededBody.DISPLAY_NAME));

            // Hard-coded false in the god-view: prototyping is per-player and there is no player context here.
            Assert.That(globalTemplate.HasBeenPrototyped, Is.False);
            Assert.That(customTemplate.HasBeenPrototyped, Is.False, "Even a player-owned template reports false, because the god-view has no player to check.");
        }
    }

    [Test, Description("A template's fitted components are projected with the slot and component they reference.")]
    public async Task TemplateFittedComponentsAreProjectedAsync()
    {
        SeededCatalogue seeded = await SeedRichCatalogueAsync();

        FullCatalogueResult catalogue = await FetchCatalogueAsync();

        DesignTemplate globalTemplate = catalogue.Templates.Single(template => template.Id == seeded.GlobalTemplateId);
        DesignTemplate customTemplate = catalogue.Templates.Single(template => template.Id == seeded.CustomTemplateId);

        DesignTemplateComponent fitted = globalTemplate.Components.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fitted.Id, Is.EqualTo(seeded.FittedComponentId));
            Assert.That(fitted.UnitComponentSlotId, Is.EqualTo(seeded.WeaponSlotId), "The fitting names the body slot it fills.");
            Assert.That(fitted.UnitComponentId, Is.EqualTo(seeded.WeaponId));
            Assert.That(fitted.ComponentCode, Is.EqualTo(SeededWeapon.CODE), "Denormalised from the fitted component.");
            Assert.That(fitted.ComponentDisplayName, Is.EqualTo(SeededWeapon.DISPLAY_NAME));
            Assert.That(fitted.ComponentType, Is.EqualTo(ComponentType.Weapon));

            Assert.That(customTemplate.Components, Is.Empty, "A template with nothing fitted gets an empty collection, not null.");
        }
    }

    [Test, Description("Returns every body, component, tag and template regardless of unlocks — no player scoping is applied.")]
    public async Task ReturnsEntireCatalogueUnfilteredAsync()
    {
        await SeedRichCatalogueAsync();

        // No PlayerUnlockedComponent or PlayerUnlockedTemplate rows exist, so an unlock-scoped read would return nothing.
        // The god-view returns everything anyway — that is the endpoint's whole purpose.
        FullCatalogueResult catalogue = await FetchCatalogueAsync();

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int unlockedComponentCount = await verifyContext.PlayerUnlockedComponents.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unlockedComponentCount, Is.Zero, "Nothing is unlocked for anybody.");
            Assert.That(catalogue.Bodies, Is.Not.Empty);
            Assert.That(catalogue.Components, Is.Not.Empty);
            Assert.That(catalogue.Tags, Is.Not.Empty);
            Assert.That(catalogue.Templates, Is.Not.Empty);
        }
    }
}
