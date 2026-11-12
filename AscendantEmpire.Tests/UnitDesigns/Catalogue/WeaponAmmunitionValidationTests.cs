using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

using static AscendantEmpire.Tests.UnitDesigns.Shared.UnitDesignPayloads;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Pins the rules a weapon's ammunition options must satisfy before anything is written.
/// </summary>
/// <remarks>
/// No database: the rules are pure functions of the payload and the asset codes that exist. That the create and update endpoints apply them, and that the
/// rows reconcile, is covered in <see cref="UpsertUnitComponentHandlerTests"/>, which has a real engine.
/// </remarks>
internal sealed class WeaponAmmunitionValidationTests
{
    private const string _ARROWS = "Weapons.Ammunition.Arrows";
    private const string _BULLETS = "Weapons.Ammunition.Bullets";
    private const string _SLING_SHOT = "Weapons.Ammunition.SlingShot";
    private const string _ARTILLERY_ROUNDS = "Weapons.Ammunition.ArtilleryRounds";

    private static readonly string[] _EVERY_CODE = [_ARROWS, _BULLETS, _SLING_SHOT, _ARTILLERY_ROUNDS];

    #region Count

    [Test, Description("A weapon carrying the maximum number of options is accepted, so the cap cannot be passing for the wrong reason.")]
    public void Validate_AcceptsOptionsUpToTheCap()
    {
        UnitComponentDto weapon = WeaponWith(Option(_ARROWS), Option(_BULLETS), Option(_SLING_SHOT));

        List<string> errors = Validate(weapon);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("A fourth option is refused naming the weapon and the cap, so authored data is never silently dropped.")]
    public void Validate_RejectsMoreOptionsThanTheCap()
    {
        UnitComponentDto weapon = WeaponWith(Option(_ARROWS), Option(_BULLETS), Option(_SLING_SHOT), Option(_ARTILLERY_ROUNDS));

        List<string> errors = Validate(weapon);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Some.Contains(WEAPON_CODE));
            Assert.That(errors, Has.Some.Contains("3"));
        }
    }

    [Test, Description("A weapon with no ammunition is accepted: a melee weapon needs none.")]
    public void Validate_AcceptsWeaponWithNoAmmunition()
    {
        List<string> errors = Validate(BuildWeapon(WEAPON_CODE));

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region References

    [Test, Description("An unknown asset code fails the save and names it, rather than throwing while the graph is built.")]
    public void Validate_RejectsUnknownAssetCode()
    {
        UnitComponentDto weapon = WeaponWith(Option("Weapons.Ammunition.NoSuchThing"));

        List<string> errors = Validate(weapon);

        Assert.That(errors, Has.Some.Contains("NoSuchThing"));
    }

    [Test, Description("An option with no asset code at all is refused, rather than looked up as an empty string.")]
    public void Validate_RejectsBlankAssetCode()
    {
        UnitComponentDto weapon = WeaponWith(Option("  "));

        List<string> errors = Validate(weapon);

        Assert.That(errors, Has.Some.Contains(WEAPON_CODE));
    }

    [Test, Description("The same Asset twice on one weapon is refused by code, rather than reaching the unique index as a DbUpdateException.")]
    public void Validate_RejectsDuplicateAssetOnOneWeapon()
    {
        UnitComponentDto weapon = WeaponWith(Option(_ARROWS), Option(_ARROWS));

        List<string> errors = Validate(weapon);

        Assert.That(errors, Has.Some.Contains(_ARROWS));
    }

    #endregion

    #region Helpers

    private static List<string> Validate(UnitComponentDto component)
    {
        List<string> errors = [];

        UnitComponentReconciler.ValidateWeaponAmmunition(component, _EVERY_CODE, errors);

        return errors;
    }

    private static UnitComponentDto WeaponWith(params UnitComponentWeaponAmmunitionDto[] ammunition)
    {
        UnitComponentDto weapon = BuildWeapon(WEAPON_CODE);

        return weapon with { Weapon = weapon.Weapon! with { Ammunition = ammunition } };
    }

    private static UnitComponentWeaponAmmunitionDto Option(string assetCode)
    {
        return new UnitComponentWeaponAmmunitionDto { AssetCode = assetCode };
    }

    #endregion
}
