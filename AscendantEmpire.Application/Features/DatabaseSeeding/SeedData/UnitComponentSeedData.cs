using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>unitdesigns.UnitComponent</c> catalogue values applied by the database seed, together with the tags those components reference.
/// </summary>
/// <remarks>
/// <para>
/// This is the second half of the starting requirements: the human body the starter templates build on, the equipment and training that give them their
/// organisation, and the primitive weapons the body's weapon slots expect. See <c>docs/design/starting-requirements.md</c>.
/// </para>
/// <para>
/// Every numeric stat uses the Scale 1,000 fixed-point convention — 1,000 raw means 1.0 — and is written raw here, because the component bulk-upsert takes raw
/// values and does no scaling of its own. That is the opposite of <see cref="BiomeSeedData"/>, whose handler scales on the way in.
/// </para>
/// <para>
/// Slots are matched by <c>Code</c> because their <c>Id</c> is left null, so re-running the seed updates the existing slot rows in place and the template
/// components fitted into them survive. Renaming a slot's code here would instead drop the old slot and everything fitted to it.
/// </para>
/// </remarks>
internal static class UnitComponentSeedData
{
    /// <summary>
    /// Builds the component catalogue payload to seed, with the tag definitions it depends on attached.
    /// </summary>
    public static BulkUpsertUnitComponentsDto Build()
    {
        return new BulkUpsertUnitComponentsDto
        {
            // Tags travel with the components: the bulk-upsert resolves each component's TagCodes against this
            // list plus whatever is already in the database, so the tags must be in the same payload.
            Tags = TagSeedData.Build(),
            Components =
            [
                BuildHumanBody(),
                BuildCampingMaterials(),
                BuildScoutTraining(),
                BuildBasicRecon(),
                BuildPrimitiveSpear(),
                BuildClub(),
            ],
        };
    }

    /// <summary>
    /// The one body in the catalogue, and the base both starter templates are built on.
    /// </summary>
    /// <remarks>
    /// Carries <c>infantry</c> so an undressed human is a commissioned infantry unit; a fitted component's organisation tag overrides that, which is how the same
    /// body becomes a settler party or a lone scout.
    /// </remarks>
    private static UnitComponentDto BuildHumanBody()
    {
        return new UnitComponentDto
        {
            Code = "human",
            DisplayName = "Human",
            Description = "A human with opposable thumbs, as impressive as they are.",
            ComponentType = ComponentType.Body,
            DesignerImagePath = "/unit-design/base/human-primitive_full-size.png",
            Weight = 1_000,
            Sight = 1_000,
            Detection = 1_000,
            TagCodes = ["infantry"],
            Body = new UnitComponentBodyDto
            {
                MovementDomain = DomainType.Land,
                CampaignMovementPoints = 1_000,

                // Derived at compile time from the campaign allowance, so it is not authored here.
                BattlefieldMovementPoints = 0,
                Power = 7000,
                Hitpoints = 0,
                Slots = BuildHumanBodySlots(),
            },
        };
    }

    /// <summary>
    /// The seven slots the human body exposes, with the UI offsets the Unit Designer draws them at.
    /// </summary>
    /// <remarks>
    /// The two that matter to the starting requirements are <c>slot-6</c> (Equipment), which the settler fits its camping materials into, and <c>training-1</c>
    /// (Training), which the scout fits its recon training into. The gap at <c>slot-3</c> is historical and deliberate.
    /// </remarks>
    private static IReadOnlyCollection<UnitComponentSlotDto> BuildHumanBodySlots()
    {
        return
        [
            new UnitComponentSlotDto
            {
                Code = "slot-1",
                DisplayName = "Primary Weapon",
                IsVisible = true,
                UIXPosition = -0.6803m,
                UIYPosition = 0.2225m,
                Rules = [ComponentTypeIn("Armour,Weapon")],
            },
            new UnitComponentSlotDto
            {
                Code = "slot-2",
                DisplayName = "Secondary Weapon",
                IsVisible = true,
                UIXPosition = 0.9655m,
                UIYPosition = 0.2254m,
                Rules = [ComponentTypeIn("Armour")],
            },
            new UnitComponentSlotDto
            {
                Code = "slot-4",
                DisplayName = "Head",
                IsVisible = true,
                UIXPosition = -0.4241m,
                UIYPosition = -0.9549m,
                Rules = [ComponentTypeEquals(ComponentType.Armour)],
            },
            new UnitComponentSlotDto
            {
                Code = "slot-5",
                DisplayName = "Legs",
                IsVisible = true,
                UIXPosition = 0.6222m,
                UIYPosition = 0.5408m,
                Rules = [ComponentTypeEquals(ComponentType.Armour)],
            },
            new UnitComponentSlotDto
            {
                Code = "slot-6",
                DisplayName = "Equipment 1",
                IsVisible = true,
                UIXPosition = 0.7366m,
                UIYPosition = -0.9521m,
                Rules = [ComponentTypeEquals(ComponentType.Equipment)],
            },
            new UnitComponentSlotDto
            {
                Code = "slot-7",
                DisplayName = "Chest",
                IsVisible = true,
                UIXPosition = 0.0118m,
                UIYPosition = -0.3324m,
                Rules = [ComponentTypeEquals(ComponentType.Armour)],
            },
            new UnitComponentSlotDto
            {
                Code = "training-1",
                DisplayName = "Training 1",
                IsVisible = true,
                UIXPosition = 0.9219m,
                UIYPosition = -0.8366m,
                Rules = [ComponentTypeEquals(ComponentType.Training)],
            },
        ];
    }

    /// <summary>
    /// The equipment that turns a human body into a settler party.
    /// </summary>
    /// <remarks>
    /// The <c>settler</c> tag on this component is the whole mechanism — the template takes its organisation from what is fitted to it. The two founding abilities
    /// ride along so a settler can plant a camp and later a settlement.
    /// </remarks>
    private static UnitComponentDto BuildCampingMaterials()
    {
        return new UnitComponentDto
        {
            Code = "camping-materials",
            DisplayName = "Camping Materials",
            ComponentType = ComponentType.Equipment,
            DesignerImagePath = "/unit-design/equipment/camping-goods_medium.png",
            Weight = 6_000,
            TagCodes = ["settler", "create-settlement", "create-early-camp"],
        };
    }

    /// <summary>
    /// Scout-tagged training that trades nothing for a doubled ability to detect other units.
    /// </summary>
    private static UnitComponentDto BuildScoutTraining()
    {
        return new UnitComponentDto
        {
            Code = "scout-training",
            DisplayName = "Scout Training",
            Description = "Field-craft.",
            ComponentType = ComponentType.Training,
            Weight = 0,
            Detection = 1_000,
            TagCodes = ["scout"],
        };
    }

    /// <summary>
    /// The scout-tagged training the starter scout template actually fits — sight rather than detection.
    /// </summary>
    /// <remarks>
    /// Shares the <c>scout</c> organisation tag with <c>scout-training</c>, so either resolves the template to the same unled organisation; this one is what the
    /// authored <c>scout</c> template carries.
    /// </remarks>
    private static UnitComponentDto BuildBasicRecon()
    {
        return new UnitComponentDto
        {
            Code = "basic-recon",
            DisplayName = "Basic Recon",
            ComponentType = ComponentType.Training,
            Weight = 0,
            Sight = 1_000,
            Detection = 0,
            TagCodes = ["scout"],
        };
    }

    /// <summary>
    /// A primitive piercing weapon for the body's primary weapon slot.
    /// </summary>
    private static UnitComponentDto BuildPrimitiveSpear()
    {
        return new UnitComponentDto
        {
            Code = "primitive-spear",
            DisplayName = "Primitive Spear",
            ComponentType = ComponentType.Weapon,
            Weight = 1_000,
            Visibility = 0,
            Weapon = new UnitComponentWeaponDto
            {
                Attack = new DamageProfileDto { Pierce = 1_000 },
                MinimumRange = 0,
                MaximumRange = 0,
            },
        };
    }

    /// <summary>
    /// A primitive crushing weapon for the body's primary weapon slot.
    /// </summary>
    private static UnitComponentDto BuildClub()
    {
        return new UnitComponentDto
        {
            Code = "club",
            DisplayName = "Club",
            ComponentType = ComponentType.Weapon,
            DesignerImagePath = @"\unit-design\weapons\melee\wood-club_120px.png",
            Weight = 0,
            Weapon = new UnitComponentWeaponDto
            {
                Attack = new DamageProfileDto { Crush = 1_000 },
                MinimumRange = 0,
                MaximumRange = 0,
            },
        };
    }

    /// <summary>
    /// Builds a slot rule accepting exactly one component type.
    /// </summary>
    private static UnitComponentSlotRuleDto ComponentTypeEquals(ComponentType componentType)
    {
        return new UnitComponentSlotRuleDto
        {
            RuleType = SlotRuleType.ComponentType,
            Operator = SlotRuleOperator.Equals,
            Value = componentType.ToString(),
        };
    }

    /// <summary>
    /// Builds a slot rule accepting any of a comma-separated list of component type names.
    /// </summary>
    /// <remarks>
    /// The value is stored as the raw list string the <c>In</c> operator splits at match time, so the names must match the <see cref="ComponentType"/> members exactly.
    /// </remarks>
    private static UnitComponentSlotRuleDto ComponentTypeIn(string commaSeparatedComponentTypes)
    {
        return new UnitComponentSlotRuleDto
        {
            RuleType = SlotRuleType.ComponentType,
            Operator = SlotRuleOperator.In,
            Value = commaSeparatedComponentTypes,
        };
    }
}
