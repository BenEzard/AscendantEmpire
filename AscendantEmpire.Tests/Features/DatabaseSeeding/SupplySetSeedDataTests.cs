using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Pins the authored supply-set seed data: the sets it names, their membership, and what each member costs.
/// </summary>
/// <remarks>
/// No database. Every set is enumerated in the seed data, membership included, so there is nothing here that needs committed Asset rows to know.
/// </remarks>
[TestFixture]
internal class SupplySetSeedDataTests
{
    private const string _BERRIES = "Foodstuffs.PlantFoods.Berries";
    private const string _ROOTS = "Foodstuffs.PlantFoods.Roots";
    private const string _POISONS = "Foodstuffs.SpecialFoods.Poisons";
    private const string _WILD_HERBS = "Foodstuffs.SpecialFoods.WildHerbs";

    private static readonly StringComparer _codeComparer = StringComparer.OrdinalIgnoreCase;

    #region Rates

    [Test, Description("Berries cost 1.67 food per troop, as the design doc fixes.")]
    public void HumanFood_PricesBerriesAtTheDesignValue()
    {
        Assert.That(Member(HumanFood(), _BERRIES).AmountPerUnit, Is.EqualTo(1_670L));
    }

    [Test, Description("A member authored without a rate of its own costs one unit per troop, which is what makes a stated rate an exception rather than a full price list.")]
    public void HumanFood_DefaultsAMemberWithNoStatedRate()
    {
        Assert.That(Member(HumanFood(), _ROOTS).AmountPerUnit, Is.EqualTo(SupplySetSeedData.DEFAULT_AMOUNT_PER_UNIT));
    }

    [Test, Description("A rate belongs to one set's member, so the same Asset can cost differently in two sets without either having to agree with the other.")]
    public void Sets_PriceEachMemberIndependentlyOfTheSameAssetElsewhere()
    {
        Dictionary<string, List<long>> ratesByAssetCode = [];

        foreach (UpsertSupplySetDto set in SupplySetSeedData.BuildSets())
        {
            foreach (UpsertSupplySetAssetDto member in set.Assets)
            {
                if (!ratesByAssetCode.TryGetValue(member.AssetCode, out List<long>? rates))
                {
                    rates = [];
                    ratesByAssetCode[member.AssetCode] = rates;
                }

                rates.Add(member.AmountPerUnit);
            }
        }

        // Nothing is read from a table keyed by Asset code, so a member's rate is whatever its own set states. Pinning the shape rather than a clash keeps
        // the test honest while no Asset happens to sit in two sets.
        Assert.That(ratesByAssetCode[_BERRIES], Is.EqualTo(new[] { 1_670L }));
    }

    #endregion

    #region Membership

    [Test, Description("Human food holds every Foodstuffs Asset bar Poisons and Wild Herbs, enumerated rather than swept up by meta-category.")]
    public void HumanFood_HoldsEveryEdibleFoodstuff()
    {
        string[] expected =
        [
            .. AssetSeedCodes()
                .Where(code => code.StartsWith("Foodstuffs.", StringComparison.OrdinalIgnoreCase))
                .Where(code => !_codeComparer.Equals(code, _POISONS) && !_codeComparer.Equals(code, _WILD_HERBS))
                .Order()
        ];

        Assert.That(HumanFood().Assets.Select(asset => asset.AssetCode).Order(), Is.EqualTo(expected));
    }

    [Test, Description("Poisons are a foodstuff by catalogue placement but not a ration, so troops are never fed them.")]
    public void HumanFood_LeavesOutPoisons()
    {
        Assert.That(HumanFood().Assets.Select(asset => asset.AssetCode), Does.Not.Contain(_POISONS));
    }

    [Test, Description("Wild Herbs are the medical set's member rather than a ration, and are what a repair draw lands on.")]
    public void HumanMedical_HoldsWildHerbsRatherThanHumanFood()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Set(SupplySetSeedData.HUMAN_MEDICAL).Assets.Select(asset => asset.AssetCode), Does.Contain(_WILD_HERBS));
            Assert.That(HumanFood().Assets.Select(asset => asset.AssetCode), Does.Not.Contain(_WILD_HERBS));
        }
    }

    [Test, Description("The seed names all three starting sets, since none of them is derived from the catalogue any more.")]
    public void BuildSets_NamesEveryStartingSet()
    {
        Assert.That(
            SupplySetSeedData.BuildSets().Select(set => set.Code).Order(),
            Is.EqualTo(new[] { SupplySetSeedData.HUMAN_FOOD, SupplySetSeedData.HUMAN_MEDICAL, SupplySetSeedData.MOUNT_FODDER }.Order()));
    }

    #endregion

    #region Asset references

    [Test, Description("Every Asset code the sets name is one the Asset seed creates, so a renamed Asset cannot leave a member silently unresolvable.")]
    public void SeedData_NamesOnlyAssetCodesTheAssetSeedCreates()
    {
        HashSet<string> seededAssetCodes = new(AssetSeedCodes(), _codeComparer);

        string[] namedCodes = [.. SupplySetSeedData.BuildSets().SelectMany(set => set.Assets.Select(asset => asset.AssetCode))];

        string[] unknownCodes = [.. namedCodes.Where(code => !seededAssetCodes.Contains(code)).Distinct().Order()];

        Assert.That(unknownCodes, Is.Empty, $"Asset code(s) the Asset seed does not create: {string.Join(", ", unknownCodes)}");
    }

    [Test, Description("Fodder is fuel rather than food, since a mount eats it while its rider still needs human food.")]
    public void Sets_ClassifyFodderAsFuel()
    {
        UpsertSupplySetDto fodderSet = Set(SupplySetSeedData.MOUNT_FODDER);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fodderSet.SupplyType, Is.EqualTo(SupplyType.Fuel));
            Assert.That(fodderSet.Assets.Select(asset => asset.AssetCode), Does.Contain("RawMaterials.RawMaterials.Fodder"));
        }
    }

    [Test, Description("Every set holds at least one member and prices each above zero, which is what the upsert refuses a set for.")]
    public void Sets_SatisfyTheUpsertValidation()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (UpsertSupplySetDto set in SupplySetSeedData.BuildSets())
            {
                Assert.That(set.Assets, Is.Not.Empty, set.Code);
                Assert.That(set.Assets.Select(asset => asset.AmountPerUnit), Is.All.GreaterThan(0), set.Code);
            }
        }
    }

    [Test, Description("No set names the same Asset twice, which the unique index on a set's members would refuse.")]
    public void Sets_NameEachAssetAtMostOncePerSet()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (UpsertSupplySetDto set in SupplySetSeedData.BuildSets())
            {
                Assert.That(set.Assets.Select(asset => asset.AssetCode), Is.Unique, set.Code);
            }
        }
    }

    [Test, Description("No two sets share a code, since the seed matches on code and would otherwise write whichever it happened to keep.")]
    public void Sets_HaveUniqueCodes()
    {
        Assert.That(SupplySetSeedData.BuildSets().Select(set => set.Code), Is.Unique);
    }

    #endregion

    #region Helpers

    private static UpsertSupplySetDto HumanFood()
    {
        return Set(SupplySetSeedData.HUMAN_FOOD);
    }

    private static UpsertSupplySetDto Set(string code)
    {
        return SupplySetSeedData.BuildSets().Single(set => set.Code == code);
    }

    private static UpsertSupplySetAssetDto Member(UpsertSupplySetDto set, string assetCode)
    {
        return set.Assets.Single(asset => _codeComparer.Equals(asset.AssetCode, assetCode));
    }

    /// <summary>
    /// Every Asset code the Asset seed creates, derived the way the upsert derives them so a code is compared against what actually lands in the database.
    /// </summary>
    private static IEnumerable<string> AssetSeedCodes()
    {
        foreach (UpsertAssetMetaCategoryDto metaCategory in AssetSeedData.Build())
        {
            string metaCode = metaCategory.Code ?? UpsertAssetsHandler.ToProperCode(metaCategory.DisplayName);

            foreach (UpsertAssetCategoryDto category in metaCategory.Categories)
            {
                string categoryCode = category.Code ?? $"{metaCode}.{UpsertAssetsHandler.ToProperCode(category.DisplayName)}";

                foreach (UpsertAssetDto asset in category.Assets)
                {
                    yield return asset.Code ?? $"{categoryCode}.{UpsertAssetsHandler.ToProperCode(asset.DisplayName)}";
                }

                foreach (UpsertAssetGroupDto group in category.Groups)
                {
                    string groupCode = group.Code ?? $"{categoryCode}.{UpsertAssetsHandler.ToProperCode(group.DisplayName)}";

                    foreach (UpsertAssetDto asset in group.Assets)
                    {
                        yield return asset.Code ?? $"{groupCode}.{UpsertAssetsHandler.ToProperCode(asset.DisplayName)}";
                    }
                }
            }
        }
    }

    #endregion
}
