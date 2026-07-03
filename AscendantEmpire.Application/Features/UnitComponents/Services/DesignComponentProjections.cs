using System.Linq.Expressions;

using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Shared EF projections from <see cref="UnitComponent"/> to the design read contracts, so the
/// unlock-filtered endpoints and the dev god-view produce identical shapes from one definition.
/// </summary>
public static class DesignComponentProjections
{
    /// <summary>Projects a Body component (with its slots and slot rules) to a <see cref="DesignBody"/>.</summary>
    public static readonly Expression<Func<UnitComponent, DesignBody>> ToDesignBody = c => new DesignBody
    {
        Id = c.Id,
        Code = c.Code,
        DisplayName = c.DisplayName,
        Description = c.Description,
        Weight = c.Weight,
        Detection = c.Detection,
        Visibility = c.Visibility,
        MovementDomain = c.Body!.MovementDomain,
        CampaignMovementPoints = c.Body.CampaignMovementPoints,
        BattlefieldMovementPoints = c.Body.BattlefieldMovementPoints,
        MinimumUnitSize = c.Body.MinimumUnitSize,
        MaximumUnitSize = c.Body.MaximumUnitSize,
        SizeIncrement = c.Body.SizeIncrement,
        TagCodes = c.Tags.Select(t => t.Tag.Code).ToList(),
        // Slots now live on the component itself, not the body extension.
        Slots = c.Slots
            .Select(s => new DesignSlot
            {
                Id = s.Id,
                DisplayName = s.DisplayName,
                IsVisible = s.IsVisible,
                IsLocked = s.IsLocked,
                UIXPosition = s.UIXPosition,
                UIYPosition = s.UIYPosition,
                Rules = s.Rules
                    .Select(r => new DesignSlotRule
                    {
                        RuleType = r.RuleType,
                        Operator = r.Operator,
                        Value = r.Value,
                    })
                    .ToList(),
            })
            .ToList(),
    };

    /// <summary>Projects a component (with its weapon/armour stats and tags) to a <see cref="DesignComponent"/>.</summary>
    public static readonly Expression<Func<UnitComponent, DesignComponent>> ToDesignComponent = c => new DesignComponent
    {
        Id = c.Id,
        Code = c.Code,
        DisplayName = c.DisplayName,
        Description = c.Description,
        ComponentType = c.ComponentType,
        Weight = c.Weight,
        Detection = c.Detection,
        Visibility = c.Visibility,
        Weapon = c.Weapon == null
            ? null
            : new DesignWeapon
            {
                Attack = new DesignDamageProfile
                {
                    Slash = c.Weapon.Attack.Slash,
                    Pierce = c.Weapon.Attack.Pierce,
                    Crush = c.Weapon.Attack.Crush,
                    Burn = c.Weapon.Attack.Burn,
                    Poison = c.Weapon.Attack.Poison,
                },
                MinimumRange = c.Weapon.MinimumRange,
                MaximumRange = c.Weapon.MaximumRange,
                DomainModifiers = c.Weapon.DomainModifiers
                    .Select(m => new DesignWeaponDomainModifier
                    {
                        Domain = m.Domain,
                        DamageMultiplier = m.DamageMultiplier,
                    })
                    .ToList(),
            },
        Armour = c.Armour == null
            ? null
            : new DesignArmour
            {
                Resistance = new DesignDamageProfile
                {
                    Slash = c.Armour.Resistance.Slash,
                    Pierce = c.Armour.Resistance.Pierce,
                    Crush = c.Armour.Resistance.Crush,
                    Burn = c.Armour.Resistance.Burn,
                    Poison = c.Armour.Resistance.Poison,
                },
            },
        // A candidate may itself expose slots (e.g. a weapon mount), so surface them for nesting.
        Slots = c.Slots
            .Select(s => new DesignSlot
            {
                Id = s.Id,
                DisplayName = s.DisplayName,
                IsVisible = s.IsVisible,
                IsLocked = s.IsLocked,
                UIXPosition = s.UIXPosition,
                UIYPosition = s.UIYPosition,
                Rules = s.Rules
                    .Select(r => new DesignSlotRule
                    {
                        RuleType = r.RuleType,
                        Operator = r.Operator,
                        Value = r.Value,
                    })
                    .ToList(),
            })
            .ToList(),
        TagCodes = c.Tags.Select(t => t.Tag.Code).ToList(),
    };

    /// <summary>
    /// Rewrites a projected slot collection so each slot's <see cref="DesignSlot.IsLocked"/> reflects the
    /// requesting player, not just the design-time flag. A slot is locked for the player when it is
    /// design-locked and the player has no active unlock row for it (see <c>PlayerUnlockedSlot</c>).
    /// </summary>
    /// <remarks>
    /// The projections above emit the raw design-time <c>IsLocked</c> (correct for the dev god-view, which
    /// has no player). Player-scoped read handlers call this to overlay per-player unlocks. Slots that are
    /// not design-locked are returned unchanged, so no allocation happens in the common case.
    /// </remarks>
    public static IReadOnlyCollection<DesignSlot> WithPlayerLockState(
        IReadOnlyCollection<DesignSlot> slots,
        IReadOnlySet<int> unlockedSlotIds)
    {
        return [.. slots.Select(slot =>
        {
            bool lockedForPlayer = slot.IsLocked && !unlockedSlotIds.Contains(slot.Id);
            return lockedForPlayer == slot.IsLocked ? slot : slot with { IsLocked = lockedForPlayer };
        })];
    }
}
