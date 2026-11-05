using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Settlements;

namespace AscendantEmpire.Tests.Features.Settlements;

/// <summary>
/// Tests for <see cref="SettlementStorageRules"/>: that every tier grants a generic pool, and that the pools rise with the ladder.
/// </summary>
/// <remarks>
/// No database. What is pinned is that no tier is left without a pool - an absent one cannot fall back to 0 without leaving a grown city unable to hold
/// anything - and that the figures are returned in the raw scaled form a balance compares against.
/// </remarks>
[TestFixture]
internal class SettlementStorageRulesTests
{
    [Test, Description("A camp's pool is the authored 2,000 units in raw scaled form, which is the figure that makes delivering to a parent the point of a camp.")]
    public void ShouldReturnTheCampPoolInRawScaledUnits()
    {
        bool found = SettlementStorageRules.TryGetGenericPoolCapacity(SettlementTier.Camp, out long rawCapacity);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(rawCapacity, Is.EqualTo(ScaledAmount.FromWhole(2_000).RawValue));
        }
    }

    [Test, Description("Every SettlementTier carries a pool, because an absent one cannot fall back to 0 without leaving a grown city unable to hold anything.")]
    public void ShouldAuthorAPoolForEverySettlementTier()
    {
        SettlementTier[] unauthored =
        [
            .. Enum.GetValues<SettlementTier>()
                .Where(tier => SettlementStorageRules.TryGetGenericPoolCapacity(tier, out _) == false)
        ];

        Assert.That(unauthored, Is.Empty);
    }

    [Test, Description("Pools rise with the tier, so growth never shrinks what a settlement can hold.")]
    public void ShouldAuthorPoolsThatRiseWithTheTier()
    {
        long[] pools =
        [
            .. Enum.GetValues<SettlementTier>()
                .OrderBy(tier => (int)tier)
                .Select(tier =>
                {
                    SettlementStorageRules.TryGetGenericPoolCapacity(tier, out long rawCapacity);

                    return rawCapacity;
                })
        ];

        Assert.That(pools, Is.Ordered.Ascending);
    }

    [Test, Description("An unmapped tier answers false with a zero capacity rather than throwing, because the only caller runs inside the end-of-Turn pipeline.")]
    public void ShouldAnswerFalseWhenTheTierIsNotAuthored()
    {
        bool found = SettlementStorageRules.TryGetGenericPoolCapacity((SettlementTier)99, out long rawCapacity);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.False);
            Assert.That(rawCapacity, Is.Zero);
        }
    }
}
