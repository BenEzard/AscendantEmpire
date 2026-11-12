using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Pins the shape rules a supply set upsert enforces before it reaches the database.
/// </summary>
/// <remarks>
/// No database: the validator is instantiated directly. Which Asset codes resolve, and whether a set's code is already taken, are the handlers' concern
/// and are tested there.
/// </remarks>
internal sealed class SupplySetValidationTests
{
    #region Members

    [Test, Description("A set with no members is refused, because a requirement naming it could never be satisfied.")]
    public void Validate_RejectsSetWithNoAssets()
    {
        UpsertSupplySetDto dto = BuildSet(SupplyType.Food, []);

        List<string> errors = SupplySetValidation.Validate(dto);

        Assert.That(errors, Has.Some.Contains("at least one Asset"));
    }

    [Test, Description("An AmountPerUnit of zero or less is refused, since it is the divisor when a need becomes an Asset amount.")]
    public void Validate_RejectsNonPositiveAmountPerUnit()
    {
        UpsertSupplySetDto dto = BuildSet(SupplyType.Food, [BuildMember("Foodstuffs.PlantFoods.Berries", amountPerUnit: 0)]);

        List<string> errors = SupplySetValidation.Validate(dto);

        Assert.That(errors, Has.Some.Contains("AmountPerUnit"));
    }

    [Test, Description("The same Asset code twice in one set is refused, naming the duplicate.")]
    public void Validate_RejectsDuplicateAssetCode()
    {
        UpsertSupplySetDto dto = BuildSet(SupplyType.Food,
        [
            BuildMember("Foodstuffs.PlantFoods.Berries"),
            BuildMember("Foodstuffs.PlantFoods.Berries")
        ]);

        List<string> errors = SupplySetValidation.Validate(dto);

        Assert.That(errors, Has.Some.Contains("Foodstuffs.PlantFoods.Berries"));
    }

    #endregion

    #region Selection weight

    [Test, Description("A negative SelectionWeight is refused; zero is allowed so a member can be excluded from the draw.")]
    public void Validate_RejectsNegativeSelectionWeight()
    {
        UpsertSupplySetDto dto = BuildSet(SupplyType.Repair,
        [
            BuildMember("RawMaterials.RawMaterials.Bone", selectionWeight: -1),
            BuildMember("Foodstuffs.SpecialFoods.WildHerbs", selectionWeight: 1)
        ]);

        List<string> errors = SupplySetValidation.Validate(dto);

        Assert.That(errors, Has.Some.Contains("SelectionWeight"));
    }

    [Test, Description("A Repair set whose every member is weighted zero is refused, because the draw would have nothing to pick.")]
    public void Validate_RejectsRepairSetWithNoPositiveWeight()
    {
        UpsertSupplySetDto dto = BuildSet(SupplyType.Repair,
        [
            BuildMember("RawMaterials.RawMaterials.Bone", selectionWeight: 0),
            BuildMember("Foodstuffs.SpecialFoods.WildHerbs", selectionWeight: 0)
        ]);

        List<string> errors = SupplySetValidation.Validate(dto);

        Assert.That(errors, Has.Some.Contains("drawn from"));
    }

    [Test, Description("A Food set weighted entirely zero is accepted, since nothing draws from it.")]
    public void Validate_AcceptsFoodSetWithNoPositiveWeight()
    {
        UpsertSupplySetDto dto = BuildSet(SupplyType.Food, [BuildMember("Foodstuffs.PlantFoods.Berries", selectionWeight: 0)]);

        List<string> errors = SupplySetValidation.Validate(dto);

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region Well-formed

    [Test, Description("A well-formed set produces no errors, so the rules above cannot be passing for the wrong reason.")]
    public void Validate_AcceptsAWellFormedSet()
    {
        UpsertSupplySetDto dto = BuildSet(SupplyType.Repair,
        [
            BuildMember("RawMaterials.RawMaterials.Bone", amountPerUnit: 2_000, selectionWeight: 3),
            BuildMember("Foodstuffs.SpecialFoods.WildHerbs", amountPerUnit: 5_000, selectionWeight: 1)
        ]);

        List<string> errors = SupplySetValidation.Validate(dto);

        Assert.That(errors, Is.Empty);
    }

    #endregion

    #region Helpers

    private static UpsertSupplySetDto BuildSet(SupplyType supplyType, IReadOnlyCollection<UpsertSupplySetAssetDto> assets)
    {
        return new UpsertSupplySetDto
        {
            Code = "TestSet",
            DisplayName = "Test Set",
            SupplyType = supplyType,
            Assets = assets
        };
    }

    private static UpsertSupplySetAssetDto BuildMember(string assetCode, long amountPerUnit = 1_000, int selectionWeight = 1)
    {
        return new UpsertSupplySetAssetDto
        {
            AssetCode = assetCode,
            AmountPerUnit = amountPerUnit,
            SelectionWeight = selectionWeight
        };
    }

    #endregion
}
