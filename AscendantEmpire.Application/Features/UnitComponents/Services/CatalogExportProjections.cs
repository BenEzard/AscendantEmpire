using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Maps loaded catalogue entities onto the bulk-upsert DTOs used as the on-disk export format.
/// </summary>
/// <remarks>
/// The reverse of <c>BulkUpsertUnitComponentsHandler</c>'s DTO to entity builders: the exported
/// <c>components.json</c> is a <see cref="BulkUpsertUnitComponentsDto"/>, so import can deserialize and
/// replay it through the existing upsert handler with no bespoke mapping. Every component must be loaded
/// with its type-specific extension, slots, rules, domain modifiers and tag joins for the projection to
/// be complete.
/// </remarks>
internal static class CatalogExportProjections
{
    /// <summary>
    /// Projects a fully-loaded component (extension + slots + rules + tags) onto its export DTO.
    /// </summary>
    public static UnitComponentDto ToUnitComponentDto(UnitComponent component)
    {
        return new UnitComponentDto
        {
            Code = component.Code,
            DisplayName = component.DisplayName,
            Description = component.Description,
            DesignerImagePath = component.DesignerImagePath,
            ComponentType = component.ComponentType,
            Weight = component.Weight,
            Detection = component.Detection,
            Visibility = component.Visibility,
            Body = component.Body is { } body ? ToBodyDto(body) : null,
            Weapon = component.Weapon is { } weapon ? ToWeaponDto(weapon) : null,
            Armour = component.Armour is { } armour ? ToArmourDto(armour) : null,
            // Tag join rows carry the Tag navigation; export just the stable codes.
            TagCodes = [.. component.Tags.Select(t => t.Tag.Code)],
        };
    }

    /// <summary>
    /// Projects a tag onto its export DTO.
    /// </summary>
    public static TagDto ToTagDto(Tag tag)
    {
        return new TagDto
        {
            Code = tag.Code,
            DisplayName = tag.DisplayName,
            Description = tag.Description,
            TagType = tag.TagType,
            // Organisation fields travel with the tag so an unled tag re-imports still unled, with its sizing.
            MovementDomain = tag.MovementDomain,
            LeadershipModel = tag.LeadershipModel,
            UnledBaseSize = tag.UnledBaseSize,
            UnledDefaultSize = tag.UnledDefaultSize,
            UnledMaxSize = tag.UnledMaxSize,
        };
    }

    private static UnitComponentBodyDto ToBodyDto(UnitComponentBody body)
    {
        return new UnitComponentBodyDto
        {
            MovementDomain = body.MovementDomain,
            CampaignMovementPoints = body.CampaignMovementPoints,
            BattlefieldMovementPoints = body.BattlefieldMovementPoints,
            Power = body.Power,
            Hitpoints = body.Hitpoints,
            // Slots are exported in slot-id order so a template's positional SlotComponentCodes round-trip.
            Slots = [.. body.Slots.OrderBy(s => s.Id).Select(ToSlotDto)],
        };
    }

    private static UnitComponentWeaponDto ToWeaponDto(UnitComponentWeapon weapon)
    {
        return new UnitComponentWeaponDto
        {
            Attack = ToDamageProfileDto(weapon.Attack),
            MinimumRange = weapon.MinimumRange,
            MaximumRange = weapon.MaximumRange,
            DualWield = weapon.DualWield,
            DomainModifiers = [.. weapon.DomainModifiers
                .OrderBy(m => m.Domain)
                .Select(m => new UnitComponentWeaponDomainModifierDto
                {
                    Domain = m.Domain,
                    DamageMultiplier = m.DamageMultiplier,
                })],
        };
    }

    private static UnitComponentArmourDto ToArmourDto(UnitComponentArmour armour)
    {
        return new UnitComponentArmourDto
        {
            Resistance = ToDamageProfileDto(armour.Resistance),
        };
    }

    private static UnitComponentSlotDto ToSlotDto(UnitComponentSlot slot)
    {
        return new UnitComponentSlotDto
        {
            // Id is intentionally left null: it is meaningless in another database and the upsert ignores it.
            DisplayName = slot.DisplayName,
            IsVisible = slot.IsVisible,
            UIXPosition = slot.UIXPosition,
            UIYPosition = slot.UIYPosition,
            Rules = [.. slot.Rules.OrderBy(r => r.Id).Select(r => new UnitComponentSlotRuleDto
            {
                RuleType = r.RuleType,
                Operator = r.Operator,
                Value = r.Value,
            })],
        };
    }

    private static DamageProfileDto ToDamageProfileDto(DamageProfile profile)
    {
        return new DamageProfileDto
        {
            Slash = profile.Slash,
            Pierce = profile.Pierce,
            Crush = profile.Crush,
            Burn = profile.Burn,
            Poison = profile.Poison,
        };
    }
}
