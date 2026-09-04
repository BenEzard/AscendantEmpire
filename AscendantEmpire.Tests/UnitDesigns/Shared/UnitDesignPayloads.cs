using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.UnitDesigns.Shared;

/// <summary>
/// The component payloads shared by the unit-design write tests.
/// </summary>
/// <remarks>
/// <para>
/// Every fixture that writes to the component catalogue needs the same handful of DTOs — a body exposing
/// some slots, and a weapon to fit into one of them — and they were previously copied between fixtures,
/// which is how they drifted: the two <c>BuildWeapon</c> copies disagreed on whether the weapon carried
/// detection, visibility and a domain modifier at all.
/// </para>
/// <para>
/// These are pure DTO builders with no database of any kind, so they sit at the lowest tier
/// <c>testing.md</c> describes and are safe to call from a fixture on any of the three.
/// </para>
/// </remarks>
internal static class UnitDesignPayloads
{
    /// <summary>
    /// The code of the body built by <see cref="BuildBodyWithSlots"/>.
    /// </summary>
    public const string BODY_CODE = "test_body";

    /// <summary>
    /// The conventional code for the weapon built by <see cref="BuildWeapon"/>.
    /// </summary>
    /// <remarks>
    /// A parameter rather than a fixed value on the builder itself, because the supersession tests need
    /// several distinct weapons at once. This constant is simply the name the single-weapon tests use.
    /// </remarks>
    public const string WEAPON_CODE = "test_weapon";

    /// <summary>
    /// Mirrors the Entity Setup Tool's slot-code derivation (<c>toCodeSlug</c> in <c>util/codeSlug.ts</c>):
    /// lowercase, every non-alphanumeric character to a hyphen, leading/trailing hyphens trimmed.
    /// </summary>
    /// <remarks>
    /// Duplicated here rather than shared, because the backend never derives a code — it only validates the
    /// one the caller sends. This exists so the test payloads look like what the tool actually posts.
    /// </remarks>
    public static string ToSlotCode(string displayName)
    {
        string hyphenated = new([.. displayName.ToLowerInvariant().Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')]);

        return hyphenated.Trim('-');
    }

    /// <summary>
    /// Builds a body slot DTO accepting a single component type, optionally carrying an existing slot id.
    /// </summary>
    /// <remarks>
    /// The id is what distinguishes an edit from a delete-plus-add on the write path, so a test that means
    /// to update an existing slot must pass the id it was given when the slot was created.
    /// </remarks>
    public static UnitComponentSlotDto BuildSlotDto(string label, string acceptedType, int? id = null, string? code = null)
    {
        return new UnitComponentSlotDto
        {
            Id = id,
            // The authoring tool derives the code from the display name, so default it the same way here.
            Code = code ?? ToSlotCode(label),
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
    public static UnitComponentDto BuildBodyWithSlots(params UnitComponentSlotDto[] slots)
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
                // Raw fixed-point (Scale 1,000): 3,000 = 3.0 campaign, 6,000 = 6.0 battlefield movement points.
                CampaignMovementPoints = 3000,
                BattlefieldMovementPoints = 6000,
                // Only bodies expose slots, so they live on the body extension.
                Slots = slots,
            },
        };
    }

    /// <summary>
    /// A standalone weapon component with the given code, a fixed attack profile and one Land modifier.
    /// </summary>
    /// <remarks>
    /// Deliberately the fuller of the two shapes this replaced: it populates the main-table Sight, Detection
    /// and Visibility columns and a domain modifier as well as the attack profile, because the catalogue graph
    /// tests assert that all of them round-trip. Tests that only need "some weapon" are unaffected by the
    /// extra fields.
    /// </remarks>
    public static UnitComponentDto BuildWeapon(string code)
    {
        return new UnitComponentDto
        {
            Code = code,
            DisplayName = "Test Weapon",
            ComponentType = ComponentType.Weapon,
            Weight = 10,
            // Raw fixed-point (Scale 1,000): Sight 500 = 0.5 tiles, deliberately different from Detection so a
            // handler reading the wrong one is caught rather than passing on a coincidentally equal value.
            Sight = 500,
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
