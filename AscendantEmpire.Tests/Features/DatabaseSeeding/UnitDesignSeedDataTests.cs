using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogueTransfer;
using AscendantEmpire.Domain.Enums.System;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that the seeded unit-design catalogue is internally coherent and satisfies everything a new game needs of it.
/// </summary>
/// <remarks>
/// <para>
/// No database — the seed-data classes are static builders and every check here is over the payloads they return, before any handler sees them.
/// </para>
/// <para>
/// These pin the wiring the seed cannot enforce for itself: the starter template codes game setup demands, the slot and component codes the templates name, and
/// the single unled organisation tag each starter must resolve to. A rename on either side of one of those links fails here rather than at game creation.
/// </para>
/// </remarks>
[TestFixture]
internal class UnitDesignSeedDataTests
{
    private readonly BulkUpsertUnitComponentsDto _components = UnitComponentSeedData.Build();
    private readonly IReadOnlyCollection<TagDto> _tags = TagSeedData.Build();
    private readonly CatalogueTemplatesDocument _templates = StarterTemplateSeedData.Build();

    #region Supply requirements

    [Test, Description("The human body requires the human food set, so a seeded database can feed its own starting units.")]
    public void ShouldGiveTheHumanBodyAFoodRequirement()
    {
        UnitComponentDto body = FindComponent("human")!;

        UnitComponentSupplyRequirementDto? food = body.SupplyRequirements
            .SingleOrDefault(requirement => requirement.SupplySetCode == SupplySetSeedData.HUMAN_FOOD);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(food, Is.Not.Null, "The body that every starter template is built on consumes nothing.");
            Assert.That(food!.Quantity, Is.GreaterThan(0));
        }
    }

    [Test, Description("Every supply set a seeded component names is one the supply set seed creates, so re-seeding cannot wipe a requirement by failing to resolve it.")]
    public void ShouldNameOnlySeededSupplySets()
    {
        HashSet<string> seededSetCodes = [.. SupplySetSeedData.BuildSets().Select(set => set.Code)];

        string[] unknownCodes = [.. _components.Components
            .SelectMany(component => component.SupplyRequirements.Select(requirement => requirement.SupplySetCode))
            .Where(code => !seededSetCodes.Contains(code))
            .Distinct()
            .Order()];

        Assert.That(unknownCodes, Is.Empty, $"Supply set code(s) the supply seed does not create: {string.Join(", ", unknownCodes)}");
    }

    [Test, Description("The Unit Components target waits on Supply Sets, because a component naming a set the database lacks fails the whole import.")]
    public void ShouldMakeUnitComponentsDependOnSupplySets()
    {
        ISeedStep step = new UnitComponentSeedStep();

        Assert.That(step.DependsOn, Does.Contain(SeedTarget.SupplySets));
    }

    #endregion

    #region Weapon ammunition

    [Test, Description("The seeded Sling accepts sling shot, so the ammunition table and its editor have a live example out of the box.")]
    public void ShouldGiveTheSlingItsAmmunition()
    {
        UnitComponentDto sling = FindComponent("sling")!;

        UnitComponentWeaponAmmunitionDto? slingShot = sling.Weapon!.Ammunition
            .SingleOrDefault(option => option.AssetCode == AssetSeedData.SLING_SHOT);

        Assert.That(slingShot, Is.Not.Null, "The one seeded weapon that needs ammunition accepts none.");
    }

    [Test, Description("Every ammunition Asset a seeded weapon names is one the Asset seed creates, so re-seeding cannot fail the whole component import.")]
    public void ShouldNameOnlySeededAmmunitionAssets()
    {
        HashSet<string> seededAssetCodes = [.. AssetSeedData.Build()
            .SelectMany(metaCategory => metaCategory.Categories)
            .SelectMany(category => category.Assets)
            .Select(asset => asset.Code!)];

        string[] unknownCodes = [.. _components.Components
            .SelectMany(component => (component.Weapon?.Ammunition ?? []).Select(option => option.AssetCode))
            .Where(code => !seededAssetCodes.Contains(code))
            .Distinct()
            .Order()];

        Assert.That(unknownCodes, Is.Empty, $"Ammunition Asset code(s) the Asset seed does not create: {string.Join(", ", unknownCodes)}");
    }

    [Test, Description("The Unit Components target waits on Assets, because a weapon naming an Asset the database lacks fails the whole import.")]
    public void ShouldMakeUnitComponentsDependOnAssets()
    {
        ISeedStep step = new UnitComponentSeedStep();

        Assert.That(step.DependsOn, Does.Contain(SeedTarget.Assets));
    }

    #endregion

    [Test, Description("Every code GameSetupService demands of a new game has a seeded template, matched the same case-insensitive way game setup matches it.")]
    public void ShouldSeedATemplateForEveryStarterCode()
    {
        IEnumerable<string> seededCodes = _templates.Templates.Select(template => template.Code);

        string[] missingCodes = [.. GameSetupService.StarterTemplateCodes.Except(seededCodes, StringComparer.OrdinalIgnoreCase)];

        Assert.That(missingCodes, Is.Empty, $"Starter codes with no seeded template: {string.Join(", ", missingCodes)}");
    }

    [Test, Description("Each seeded template names a base that is a seeded Body component, so the bulk upsert can assemble it.")]
    public void ShouldBuildEveryTemplateOnASeededBody()
    {
        foreach (CatalogueTemplateDto template in _templates.Templates)
        {
            UnitComponentDto? body = FindComponent(template.BaseCode);

            Assert.That(body, Is.Not.Null, $"Template '{template.Code}' names base '{template.BaseCode}', which is not seeded.");
            Assert.That(body.ComponentType, Is.EqualTo(ComponentType.Body), $"Template '{template.Code}' names base '{template.BaseCode}', which is not a Body.");
        }
    }

    [Test, Description("Each fitted component sits in a slot its base body actually exposes and satisfies that slot's acceptance rules.")]
    public void ShouldFitEveryTemplateComponentIntoASlotThatAcceptsIt()
    {
        foreach (CatalogueTemplateDto template in _templates.Templates)
        {
            UnitComponentDto body = FindComponent(template.BaseCode)!;

            foreach (KeyValuePair<string, string> fitting in template.SlotComponentCodes)
            {
                UnitComponentSlotDto? slot = body.Body!.Slots.SingleOrDefault(candidate => candidate.Code == fitting.Key);
                Assert.That(slot, Is.Not.Null, $"Template '{template.Code}' fits into slot '{fitting.Key}', which body '{body.Code}' does not expose.");

                UnitComponentDto? fitted = FindComponent(fitting.Value);
                Assert.That(fitted, Is.Not.Null, $"Template '{template.Code}' fits component '{fitting.Value}', which is not seeded.");

                Assert.That(SlotAccepts(slot, fitted.ComponentType), Is.True,
                    $"Template '{template.Code}' fits {fitted.ComponentType} '{fitted.Code}' into slot '{slot.Code}', which does not accept that type.");
            }
        }
    }

    [Test, Description("Every tag a seeded component references is seeded by the tag step, which the components step declares a dependency on.")]
    public void ShouldDefineEveryTagItsComponentsReference()
    {
        IEnumerable<string> definedCodes = _tags.Select(tag => tag.Code);

        string[] undefinedCodes = [.. _components.Components.SelectMany(component => component.TagCodes).Distinct().Except(definedCodes)];

        Assert.That(undefinedCodes, Is.Empty, $"Tags referenced by a seeded component but not defined: {string.Join(", ", undefinedCodes)}");
    }

    [Test, Description("Every seeded tag passes the same organisation-field rules the Tag manager and the bulk upsert apply, so the seed cannot author an invalid tag.")]
    public void ShouldSeedOnlyTagsThatPassOrganisationValidation()
    {
        foreach (TagDto tag in _tags)
        {
            List<string> errors = TagValidation.ValidateOrganisationFields(
                tag.TagType,
                tag.MovementDomain,
                tag.LeadershipModel,
                tag.UnledBaseSize,
                tag.UnledDefaultSize,
                tag.UnledMaxSize,
                tag.Code);

            Assert.That(errors, Is.Empty, string.Join("; ", errors));
        }
    }

    [Test, Description("Each starter template resolves to exactly one organisation tag, which is Unled with a default size — the three things game setup refuses to spawn without.")]
    public void ShouldResolveEveryStarterTemplateToOneUnledOrganisationTag()
    {
        foreach (CatalogueTemplateDto template in _templates.Templates)
        {
            // A fitted component's organisation tag overrides the body's, so the fitted ones alone decide the
            // organisation — and two different ones would make it ambiguous and fail compilation.
            TagDto[] organisationTags =
            [
                .. template.SlotComponentCodes.Values
                    .SelectMany(componentCode => FindComponent(componentCode)!.TagCodes)
                    .Distinct()
                    .Select(FindTag)
                    .Where(tag => tag!.TagType == TagType.MilitaryOrganisationalGroup)!
            ];

            Assert.That(organisationTags, Has.Length.EqualTo(1),
                $"Template '{template.Code}' resolves to {organisationTags.Length} organisation tag(s): {string.Join(", ", organisationTags.Select(tag => tag.Code))}");

            TagDto organisationTag = organisationTags[0];

            Assert.That(organisationTag.LeadershipModel, Is.EqualTo(LeadershipModel.Unled), $"Starter template '{template.Code}' resolves to a commissioned tag.");
            Assert.That(organisationTag.UnledDefaultSize, Is.Not.Null.And.GreaterThan(0), $"Starter template '{template.Code}' has no spawn size to use.");
        }
    }

    [Test, Description("Slot codes are unique within their body, because a template names the slot it fits into by code alone.")]
    public void ShouldGiveEveryBodyDistinctSlotCodes()
    {
        foreach (UnitComponentDto body in _components.Components.Where(component => component.ComponentType == ComponentType.Body))
        {
            string[] slotCodes = [.. body.Body!.Slots.Select(slot => slot.Code)];

            Assert.That(slotCodes, Is.Unique, $"Body '{body.Code}' has duplicate slot codes.");
        }
    }

    /// <summary>
    /// Finds a seeded component by code, or null when nothing is seeded under that code.
    /// </summary>
    private UnitComponentDto? FindComponent(string code)
    {
        return _components.Components.SingleOrDefault(component => component.Code == code);
    }

    /// <summary>
    /// Finds a seeded tag by code, or null when nothing is seeded under that code.
    /// </summary>
    private TagDto? FindTag(string code)
    {
        return _tags.SingleOrDefault(tag => tag.Code == code);
    }

    /// <summary>
    /// Whether a slot's rules admit a component of the given type.
    /// </summary>
    /// <remarks>
    /// Mirrors the component-type rules the seed authors — every rule must pass. Tag rules are not seeded, so a <see cref="SlotRuleType.HasTag"/> rule is treated
    /// as unmet rather than silently accepted, which fails the test if one is ever added without this keeping up.
    /// </remarks>
    private static bool SlotAccepts(UnitComponentSlotDto slot, ComponentType componentType)
    {
        return slot.Rules.All(rule => RuleAccepts(rule, componentType));
    }

    /// <summary>
    /// Whether one slot rule admits a component of the given type.
    /// </summary>
    private static bool RuleAccepts(UnitComponentSlotRuleDto rule, ComponentType componentType)
    {
        if (rule.RuleType != SlotRuleType.ComponentType)
        {
            return false;
        }

        string typeName = componentType.ToString();

        return rule.Operator switch
        {
            SlotRuleOperator.Equals => rule.Value == typeName,
            SlotRuleOperator.NotEquals => rule.Value != typeName,
            SlotRuleOperator.In => rule.Value.Split(',').Select(value => value.Trim()).Contains(typeName),
            _ => false,
        };
    }
}
