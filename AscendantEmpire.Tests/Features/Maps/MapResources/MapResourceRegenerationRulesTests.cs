using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins how a renewable MapResource grows, applies the Round's spreads, and goes dormant, reseeds or is deleted at the end of a Round.
/// </summary>
/// <remarks>
/// No database: <see cref="MapResourceRegenerationRules"/> is pure, so every step can be pinned directly. The spreads themselves are pinned in <c>MapResourceSpreadRulesTests</c>.
/// </remarks>
[TestFixture]
internal sealed class MapResourceRegenerationRulesTests
{
    private const long _SEED = 20261119;
    private const int _BERRY_TYPE_ID = 11;
    private const int _WOOD_TYPE_ID = 7;
    private const long _CEILING = 10_000_000;
    private const decimal _GROWTH_PERCENTAGE = 0.1m;
    private const int _DORMANT_SINCE_ROUND = 10;
    private const int _SURVIVAL_ROUNDS = 3;

    #region Growth
    [Test, Description("A small quantity gains the minimum rather than a share that rounds to almost nothing, or it would never recover.")]
    public void ShouldGrowByTheMinimumWhenTheShareIsSmaller()
    {
        Assert.That(MapResourceRegenerationRules.Grow(quantity: 2_000, maximumQuantity: _CEILING, _GROWTH_PERCENTAGE), Is.EqualTo(2_000 + MapResourceRegenerationRules.MINIMUM_GROWTH));
    }

    [Test, Description("Growth stops at the ceiling rather than overshooting it, so a quantity just below it lands on it exactly.")]
    public void ShouldCapGrowthAtTheCeiling()
    {
        Assert.That(MapResourceRegenerationRules.Grow(quantity: 9_500_000, maximumQuantity: _CEILING, _GROWTH_PERCENTAGE), Is.EqualTo(_CEILING));
    }

    [Test, Description("What flooring the shares drops goes to the shares that lost the most, so the total grows by exactly the growth and no Asset is short-changed by more than one unit.")]
    public void ShouldGiveTheLeftoverToTheLargestRemaindersWhenSharingDistributionGrowth()
    {
        IReadOnlyCollection<DistributedQuantity> grown = MapResourceRegenerationRules.GrowDistribution([Amount(1, 1_000), Amount(2, 2_000), Amount(3, 4_000)], _CEILING, _GROWTH_PERCENTAGE);

        Assert.That(grown, Is.EqualTo(new[] { Amount(1, 1_143), Amount(2, 2_286), Amount(3, 4_571) }));
    }

    [Test, Description("Equal shares tie on what they lost, and the lowest AssetId takes the leftover so the result does not depend on the order the amounts were read in.")]
    public void ShouldGiveTheLeftoverToTheLowestAssetIdWhenTheRemaindersTie()
    {
        IReadOnlyCollection<DistributedQuantity> grown = MapResourceRegenerationRules.GrowDistribution([Amount(5, 1_000), Amount(3, 1_000), Amount(9, 1_000)], _CEILING, _GROWTH_PERCENTAGE);

        Assert.That(grown, Is.EqualTo(new[] { Amount(5, 1_333), Amount(3, 1_334), Amount(9, 1_333) }));
    }

    [Test, Description("An Asset gone from a tile gets no share of growth, so a wood felled out of a forest never comes back.")]
    public void ShouldLeaveAnAmountAtZeroWhenSharingDistributionGrowth()
    {
        IReadOnlyCollection<DistributedQuantity> grown = MapResourceRegenerationRules.GrowDistribution([Amount(1, 0), Amount(2, 3_000)], _CEILING, _GROWTH_PERCENTAGE);

        Assert.That(grown.Single(amount => amount.AssetId == 1).Amount, Is.Zero);
    }

    [Test, Description("A Distribution's ceiling caps its total, and the leftover rule lets the total land on it exactly instead of stopping just short.")]
    public void ShouldLandADistributionTotalOnItsCeiling()
    {
        IReadOnlyCollection<DistributedQuantity> grown = MapResourceRegenerationRules.GrowDistribution([Amount(1, 1_000), Amount(2, 2_000)], maximumQuantity: 3_500, _GROWTH_PERCENTAGE);

        Assert.That(grown.Sum(amount => amount.Amount), Is.EqualTo(3_500));
    }

    [Test, Description("A Distribution born above its ceiling stops growing, but nothing cuts it back to the ceiling.")]
    public void ShouldLeaveADistributionAboveItsCeilingUnchanged()
    {
        IReadOnlyCollection<DistributedQuantity> amounts = [Amount(1, 2_000), Amount(2, 3_000)];

        Assert.That(MapResourceRegenerationRules.GrowDistribution(amounts, maximumQuantity: 4_000, _GROWTH_PERCENTAGE), Is.EqualTo(amounts));
    }

    [Test, Description("A Distribution grows its amounts rather than a Quantity, which it does not hold.")]
    public void ShouldGrowTheAmountsWhenTheCandidateIsADistribution()
    {
        MapResourceRegenerationCandidate candidate = Candidate(Instance(quantity: null, amounts: [Amount(1, 1_000), Amount(2, 2_000)]), WoodHabitat(), RegenerationCandidateType.Grow);

        MapResourceRegenerationRules.ApplyGrowth(candidate);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(candidate.Quantity, Is.Null);
            Assert.That(candidate.InstanceAmounts.Sum(amount => amount.Amount), Is.EqualTo(4_000));
        }
    }
    #endregion

    #region Applying spread
    [Test, Description("Growth runs on the quantity a spread left, so a patch topped up this Round grows from the topped-up figure.")]
    public void ShouldSetTheQuantityWhenASpreadTopsUpAnInstance()
    {
        MapResourceRegenerationCandidate target = Candidate(Instance(quantity: 1_000_000), BerryHabitat(), RegenerationCandidateType.Grow);

        MapResourceRegenerationRules.ApplySpreadPlan(ById(target), new MapResourceSpreadPlan { TopUps = [new SpreadTopUp { MapResourceInstanceId = target.Instance.MapResourceInstanceId, Quantity = 1_100_000 }] });

        Assert.That(target.Quantity, Is.EqualTo(1_100_000));
    }

    [Test, Description("A revived instance is live again, so its dormancy is cleared and regrowth must leave it alone despite its classification.")]
    public void ShouldClearDormancyWhenASpreadRevivesAnInstance()
    {
        MapResourceRegenerationCandidate target = Candidate(Instance(quantity: 0, dormantSinceRound: _DORMANT_SINCE_ROUND, spreadLimit: 0), BerryHabitat(), RegenerationCandidateType.Revive);

        MapResourceRegenerationRules.ApplySpreadPlan(
            ById(target),
            new MapResourceSpreadPlan { Revivals = [new SpreadRevival { MapResourceInstanceId = target.Instance.MapResourceInstanceId, Quantity = 1_000_000, SpreadLimit = 2 }] });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.Quantity, Is.EqualTo(1_000_000));
            Assert.That(target.SpreadLimit, Is.EqualTo(2));
            Assert.That(target.DormantSinceRound, Is.Null);
            Assert.That(target.IsRevivedBySpread, Is.True);
        }
    }

    [Test, Description("Each spread that is used up comes off the spreader's limit, so a generation can only spread so far.")]
    public void ShouldTakeTheSpreadsUsedOffTheSpreadersLimit()
    {
        MapResourceRegenerationCandidate spreader = Candidate(Instance(quantity: _CEILING, spreadLimit: 3), BerryHabitat(), RegenerationCandidateType.Spread);

        MapResourceRegenerationRules.ApplySpreadPlan(ById(spreader), new MapResourceSpreadPlan { SpreadsUsed = new Dictionary<int, int> { [spreader.Instance.MapResourceInstanceId] = 2 } });

        Assert.That(spreader.SpreadLimit, Is.EqualTo(1));
    }

    [Test, Description("Every instance a spread reaches passed the same habitat check pairing did, so one missing from the candidates is a defect worth failing on.")]
    public void ShouldThrowWhenTheSpreadPlanNamesAnUnknownInstance()
    {
        Assert.That(
            () => MapResourceRegenerationRules.ApplySpreadPlan(new Dictionary<int, MapResourceRegenerationCandidate>(), new MapResourceSpreadPlan { TopUps = [new SpreadTopUp { MapResourceInstanceId = 99 }] }),
            Throws.InvalidOperationException);
    }
    #endregion

    #region Regrowth
    [Test, Description("An instance just found at 0 goes dormant; its first chance to reseed is the following Round.")]
    public void ShouldGoDormantWhenJustExhausted()
    {
        MapResourceRegenerationCandidate candidate = Candidate(Instance(quantity: 0, reseedLikelihood: 1m), BerryHabitat(), RegenerationCandidateType.Revive);

        Assert.That(MapResourceRegenerationRules.DecideRegrowth(candidate, _SEED, currentRoundNumber: _DORMANT_SINCE_ROUND), Is.EqualTo(RegrowthOutcome.GoesDormant));
    }

    [Test, Description("A type that survives no Rounds is deleted the moment it is exhausted, with no dormant Round to roll in.")]
    public void ShouldDeleteAtOnceWhenTheTypeSurvivesNoRounds()
    {
        MapResourceRegenerationCandidate candidate = Candidate(Instance(quantity: 0, survivalRounds: 0, reseedLikelihood: 1m), BerryHabitat(), RegenerationCandidateType.Revive);

        Assert.That(MapResourceRegenerationRules.DecideRegrowth(candidate, _SEED, currentRoundNumber: _DORMANT_SINCE_ROUND), Is.EqualTo(RegrowthOutcome.Deleted));
    }

    [Test, Description("Ending the same Round again must not roll or age an instance it already stamped dormant.")]
    public void ShouldStayDormantWhenTheRoundIsTheOneItWentDormantIn()
    {
        MapResourceRegenerationCandidate candidate = Candidate(Instance(quantity: 0, dormantSinceRound: _DORMANT_SINCE_ROUND, reseedLikelihood: 1m), BerryHabitat(), RegenerationCandidateType.Revive);

        Assert.That(MapResourceRegenerationRules.DecideRegrowth(candidate, _SEED, currentRoundNumber: _DORMANT_SINCE_ROUND), Is.EqualTo(RegrowthOutcome.StaysDormant));
    }

    [Test, Description("A dormant instance whose roll succeeds comes back.")]
    public void ShouldReseedWhenTheRollSucceeds()
    {
        MapResourceRegenerationCandidate candidate = Candidate(Instance(quantity: 0, dormantSinceRound: _DORMANT_SINCE_ROUND, reseedLikelihood: 1m), BerryHabitat(), RegenerationCandidateType.Revive);

        Assert.That(MapResourceRegenerationRules.DecideRegrowth(candidate, _SEED, currentRoundNumber: _DORMANT_SINCE_ROUND + 1), Is.EqualTo(RegrowthOutcome.Reseeds));
    }

    [TestCase(_DORMANT_SINCE_ROUND + 1, RegrowthOutcome.StaysDormant, Description = "A failed roll before its survival has run out leaves it dormant for another try.")]
    [TestCase(_DORMANT_SINCE_ROUND + _SURVIVAL_ROUNDS - 1, RegrowthOutcome.StaysDormant, Description = "Its last but one dormant Round still leaves it a roll to come.")]
    [TestCase(_DORMANT_SINCE_ROUND + _SURVIVAL_ROUNDS, RegrowthOutcome.Deleted, Description = "Failing the roll in its last dormant Round deletes it, having had one roll for every survival Round.")]
    public void ShouldDeleteOnlyOnceEverySurvivalRoundHasFailed(int currentRoundNumber, RegrowthOutcome expected)
    {
        MapResourceRegenerationCandidate candidate = Candidate(Instance(quantity: 0, dormantSinceRound: _DORMANT_SINCE_ROUND, reseedLikelihood: 0m), BerryHabitat(), RegenerationCandidateType.Revive);

        Assert.That(MapResourceRegenerationRules.DecideRegrowth(candidate, _SEED, currentRoundNumber), Is.EqualTo(expected));
    }

    [Test, Description("An exhausted Distribution has no amounts to share a reseed between, so it never comes back however likely the roll.")]
    public void ShouldNeverReseedWhenTheCandidateIsADistribution()
    {
        MapResourceRegenerationCandidate candidate = Candidate(
            Instance(quantity: null, dormantSinceRound: _DORMANT_SINCE_ROUND, reseedLikelihood: 1m, amounts: [Amount(1, 0)]),
            WoodHabitat(),
            RegenerationCandidateType.Revive);

        Assert.That(MapResourceRegenerationRules.DecideRegrowth(candidate, _SEED, currentRoundNumber: _DORMANT_SINCE_ROUND + 1), Is.EqualTo(RegrowthOutcome.StaysDormant));
    }

    [Test, Description("A reseeded instance comes back at its growth share of its ceiling, and is no longer dormant.")]
    public void ShouldRestoreTheQuantityAndClearDormancyWhenItReseeds()
    {
        MapResourceRegenerationCandidate candidate = Candidate(Instance(quantity: 0, dormantSinceRound: _DORMANT_SINCE_ROUND, reseedLikelihood: 1m), BerryHabitat(), RegenerationCandidateType.Revive);

        MapResourceRegenerationRules.ApplyRegrowth(candidate, _SEED, currentRoundNumber: _DORMANT_SINCE_ROUND + 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(candidate.Quantity, Is.EqualTo(1_000_000));
            Assert.That(candidate.DormantSinceRound, Is.Null);
        }
    }

    [Test, Description("The reseed roll is drawn from the seed, the instance and the Round, so the same dormant instance can fail one Round and succeed another.")]
    public void ShouldRollDifferentlyFromRoundToRound()
    {
        MapResourceRegenerationCandidate candidate = Candidate(
            Instance(quantity: 0, dormantSinceRound: _DORMANT_SINCE_ROUND, survivalRounds: 1_000, reseedLikelihood: 0.5m),
            BerryHabitat(),
            RegenerationCandidateType.Revive);

        RegrowthOutcome[] outcomes = [.. Enumerable.Range(_DORMANT_SINCE_ROUND + 1, 40).Select(round => MapResourceRegenerationRules.DecideRegrowth(candidate, _SEED, round))];

        Assert.That(outcomes.Distinct(), Is.EquivalentTo(new[] { RegrowthOutcome.Reseeds, RegrowthOutcome.StaysDormant }));
    }
    #endregion

    #region Building the plan
    [Test, Description("Only what regeneration changed is written, so an instance at its ceiling that did not spread costs no update.")]
    public void ShouldWriteNoUpdateWhenNothingChanged()
    {
        MapResourceRegenerationCandidate unchanged = Candidate(Instance(quantity: _CEILING, spreadLimit: 0), BerryHabitat(), RegenerationCandidateType.Spread);

        MapResourceRegenerationPlan plan = MapResourceRegenerationRules.BuildPlan([unchanged], new MapResourceSpreadPlan());

        Assert.That(plan.InstanceUpdates, Is.Empty);
    }

    [Test, Description("A deleted instance is deleted, not also updated, since the update would name a row the delete removed.")]
    public void ShouldDeleteRatherThanUpdateWhenTheCandidateWasDeleted()
    {
        MapResourceRegenerationCandidate deleted = Candidate(Instance(quantity: 0, survivalRounds: 0), BerryHabitat(), RegenerationCandidateType.Revive);
        MapResourceRegenerationRules.ApplyRegrowth(deleted, _SEED, _DORMANT_SINCE_ROUND);

        MapResourceRegenerationPlan plan = MapResourceRegenerationRules.BuildPlan([deleted], new MapResourceSpreadPlan());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.DeletedMapResourceInstanceIds, Is.EqualTo(new[] { deleted.Instance.MapResourceInstanceId }));
            Assert.That(plan.InstanceUpdates, Is.Empty);
        }
    }

    [Test, Description("A Distribution's grown amounts are written row by row, and an amount growth left alone is not rewritten.")]
    public void ShouldWriteOnlyTheAmountsGrowthChanged()
    {
        MapResourceRegenerationCandidate wood = Candidate(Instance(quantity: null, amounts: [Amount(1, 0), Amount(2, 3_000)]), WoodHabitat(), RegenerationCandidateType.Grow);
        MapResourceRegenerationRules.ApplyGrowth(wood);

        MapResourceRegenerationPlan plan = MapResourceRegenerationRules.BuildPlan([wood], new MapResourceSpreadPlan());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.AmountUpdates.Select(update => update.AssetId), Is.EqualTo(new[] { 2 }));
            Assert.That(plan.InstanceUpdates, Is.Empty);
        }
    }
    #endregion

    private static RenewableHabitat BerryHabitat()
    {
        return new RenewableHabitat
        {
            MapResourceTypeId = _BERRY_TYPE_ID,
            BiomeTypeId = BiomeType.Forest,
            MaximumQuantity = _CEILING,
            GrowthPercentage = _GROWTH_PERCENTAGE,
            SpreadLikelihood = 0.5m
        };
    }

    private static RenewableHabitat WoodHabitat()
    {
        return new RenewableHabitat
        {
            MapResourceTypeId = _WOOD_TYPE_ID,
            IsDistribution = true,
            BiomeTypeId = BiomeType.Forest,
            MaximumQuantity = _CEILING,
            GrowthPercentage = _GROWTH_PERCENTAGE
        };
    }

    private static MapResourceRegenerationInstance Instance(
        long? quantity,
        int? dormantSinceRound = null,
        int? spreadLimit = 2,
        int survivalRounds = _SURVIVAL_ROUNDS,
        decimal reseedLikelihood = 0.5m,
        IReadOnlyCollection<DistributedQuantity>? amounts = null)
    {
        return new MapResourceRegenerationInstance
        {
            MapResourceInstanceId = 501,
            MapResourceTypeId = amounts == null ? _BERRY_TYPE_ID : _WOOD_TYPE_ID,
            BiomeTypeId = BiomeType.Forest,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Quantity = quantity,
            SpreadLimit = amounts == null ? spreadLimit : null,
            DormantSinceRound = dormantSinceRound,
            SurvivalRounds = survivalRounds,
            ReseedLikelihood = reseedLikelihood,
            InstanceAmounts = amounts ?? []
        };
    }

    private static MapResourceRegenerationCandidate Candidate(MapResourceRegenerationInstance instance, RenewableHabitat habitat, RegenerationCandidateType regenerationType)
    {
        return new MapResourceRegenerationCandidate(instance, habitat, regenerationType);
    }

    private static Dictionary<int, MapResourceRegenerationCandidate> ById(MapResourceRegenerationCandidate candidate)
    {
        return new Dictionary<int, MapResourceRegenerationCandidate> { [candidate.Instance.MapResourceInstanceId] = candidate };
    }

    private static DistributedQuantity Amount(int assetId, long amount)
    {
        return new DistributedQuantity { AssetId = assetId, Amount = amount };
    }
}
