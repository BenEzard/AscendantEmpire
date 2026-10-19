using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Settlements;

namespace AscendantEmpire.Tests.Features.Settlements;

/// <summary>
/// Tests for <see cref="SettlementTierRules"/>: that each foundable tier points at the action whose rules judge it and the Structure that stands for it.
/// </summary>
/// <remarks>
/// No database. This is the single join between three things that live apart - the action type, the strategy behind it, and the Structure catalogue - so what is pinned here
/// is that the join is right and that the higher tiers are refused rather than quietly mapped to something.
/// </remarks>
[TestFixture]
internal class SettlementTierRulesTests
{
    [Test, Description("A village maps to the Create Village action and the village Structure, so the handler judges it by the strategy that rendered the button.")]
    public void ShouldMapVillageToItsActionAndStructure()
    {
        SettlementTierDefinition? definition = SettlementTierRules.For(SettlementTier.Village);

        Assert.That(definition, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(definition!.ActionType, Is.EqualTo(ForceActionType.CreateVillage));
            Assert.That(definition.StructureCode, Is.EqualTo(StructureSeedData.VILLAGE_CODE));
        }
    }

    [Test, Description("A camp maps to the Establish Camp action and the camp Structure, which is what keeps the two tiers from sharing one set of rules.")]
    public void ShouldMapCampToItsActionAndStructure()
    {
        SettlementTierDefinition? definition = SettlementTierRules.For(SettlementTier.Camp);

        Assert.That(definition, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(definition!.ActionType, Is.EqualTo(ForceActionType.EstablishCamp));
            Assert.That(definition.StructureCode, Is.EqualTo(StructureSeedData.CAMP_CODE));
        }
    }

    [Test, Description("The tiers reached by growth cannot be founded directly, so asking for one is a refusal rather than a missing case the handler would fall through.")]
    [TestCase(SettlementTier.Hamlet, Description = "Hamlet is reached by growth, which is not built.")]
    [TestCase(SettlementTier.Town, Description = "Town is reached by growth, which is not built.")]
    [TestCase(SettlementTier.City, Description = "City is reached by growth, which is not built.")]
    [TestCase(SettlementTier.MajorCity, Description = "Major City is reached by growth, which is not built.")]
    public void ShouldNotMapATierThatCannotBeFounded(SettlementTier tier)
    {
        Assert.That(SettlementTierRules.For(tier), Is.Null);
    }

    [Test, Description("Only the two settler tiers are foundable, which is the list the refusal message names back to the player.")]
    public void ShouldOfferExactlyTheTwoSettlerTiersAsFoundable()
    {
        Assert.That(SettlementTierRules.FoundableTiers, Is.EquivalentTo(new[] { SettlementTier.Camp, SettlementTier.Village }));
    }

    [Test, Description("No two tiers share a Structure code, since the code is what the map draws and the handler looks up.")]
    public void ShouldGiveEachFoundableTierItsOwnStructureCode()
    {
        string[] codes = [.. SettlementTierRules.FoundableTiers.Select(tier => SettlementTierRules.For(tier)!.StructureCode)];

        Assert.That(codes, Is.Unique);
    }

    [Test, Description("Every tier's Structure code is in the seeded catalogue, so a founding cannot fail on a code nothing writes.")]
    public void ShouldNameOnlyStructureCodesTheSeedWrites()
    {
        string[] seededCodes = [.. StructureSeedData.Build().Select(structure => structure.Code)];

        foreach (SettlementTier tier in SettlementTierRules.FoundableTiers)
        {
            Assert.That(seededCodes, Does.Contain(SettlementTierRules.For(tier)!.StructureCode));
        }
    }
}
