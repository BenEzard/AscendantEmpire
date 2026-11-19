using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Helpers.Randomness;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Decides how a Game's renewable MapResources regenerate at the end of a Round.
/// </summary>
/// <remarks>
/// Pure and in-memory, as <see cref="MapResourceInstantiationRules"/> is, so every rule can be pinned without a database. See the End of round order in
/// <c>docs/design/campaign-map/map-resources.md</c>.
/// </remarks>
internal static class MapResourceRegenerationRules
{
    /// <summary>
    /// The least a growing quantity gains in one step, stored scaled by <c>ScaledAmount.Scale</c>, so a small quantity does not stall on rounding.
    /// </summary>
    internal const long MINIMUM_GROWTH = 1_000;

    /// <summary>
    /// How much a quantity gains in one step of growth: its share at the growth percentage, but never less than <see cref="MINIMUM_GROWTH"/>.
    /// </summary>
    /// <remarks>
    /// The share is floored, so growth never rounds up past what the percentage gives.
    /// </remarks>
    internal static long GrowthAmount(long quantity, decimal growthPercentage)
    {
        return Math.Max(MINIMUM_GROWTH, (long)(quantity * growthPercentage));
    }

    /// <summary>
    /// A quantity after one step of growth, capped at its ceiling.
    /// </summary>
    internal static long Grow(long quantity, long maximumQuantity, decimal growthPercentage)
    {
        return Math.Min(maximumQuantity, quantity + GrowthAmount(quantity, growthPercentage));
    }

    /// <summary>
    /// A Distribution's amounts after one step of growth: the total grows as a single quantity would, shared in proportion to the current amounts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by <see cref="ProportionalSplitter"/>, so the shares sum to exactly the growth and the total can land on its ceiling rather than creeping towards it.
    /// </para>
    /// <para>
    /// An amount at 0 gets no share, so an Asset gone from the tile never comes back. The amounts come back in the order they were given.
    /// </para>
    /// </remarks>
    internal static IReadOnlyCollection<DistributedQuantity> GrowDistribution(IReadOnlyCollection<DistributedQuantity> amounts, long maximumQuantity, decimal growthPercentage)
    {
        long total = amounts.Sum(amount => amount.Amount);

        if (total <= 0)
        {
            return amounts;
        }

        // Negative for a total above its ceiling, which is left as it is rather than cut back.
        long growth = Grow(total, maximumQuantity, growthPercentage) - total;

        if (growth <= 0)
        {
            return amounts;
        }

        Dictionary<int, long> shares = ProportionalSplitter.Split(growth, amounts.ToDictionary(amount => amount.AssetId, amount => amount.Amount));

        return [.. amounts.Select(amount => amount with { Amount = amount.Amount + shares[amount.AssetId] })];
    }

    /// <summary>
    /// The habitat of a type that covers a tile's Biome and Geography, or <see langword="null"/> when none does.
    /// </summary>
    /// <remarks>
    /// Precedence between a Geography-specific habitat and a Biome-wide one is <see cref="BiomeImpliedResourceRules.FindHabitat"/>'s, not decided here.
    /// </remarks>
    /// <param name="habitatsByType">Each renewable type's habitats keyed by scope, themselves keyed by <c>MapResourceTypeId</c>.</param>
    internal static RenewableHabitat? FindHabitat(
        IReadOnlyDictionary<int, Dictionary<HabitatScope, RenewableHabitat>> habitatsByType,
        int mapResourceTypeId,
        BiomeType biome,
        CampaignMapGeographyType geography)
    {
        if (habitatsByType.TryGetValue(mapResourceTypeId, out Dictionary<HabitatScope, RenewableHabitat>? habitatsForType) == false)
        {
            return null;
        }

        HabitatScope? scope = BiomeImpliedResourceRules.FindHabitat(habitatsForType.Keys, biome, geography);

        return scope == null ? null : habitatsForType[scope.Value];
    }

    /// <summary>
    /// Which step an instance takes this Round, from its quantity as read against its ceiling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Distribution is classified by the total of its amounts, since it holds no <c>Quantity</c> of its own.
    /// </para>
    /// <para>
    /// A missing quantity throws rather than defaulting: a quantity read as 0 would send a live instance to <see cref="RegenerationCandidateType.Revive"/>.
    /// </para>
    /// </remarks>
    internal static RegenerationCandidateType Classify(MapResourceRegenerationInstance instance, RenewableHabitat habitat)
    {
        long quantity = habitat.IsDistribution
            ? instance.InstanceAmounts.Sum(amount => amount.Amount)
            : instance.Quantity ?? throw new InvalidOperationException($"MapResourceInstance {instance.MapResourceInstanceId} is a single-quantity renewable with no Quantity.");

        if (quantity == 0)
        {
            return RegenerationCandidateType.Revive;
        }

        return quantity >= habitat.MaximumQuantity ? RegenerationCandidateType.Spread : RegenerationCandidateType.Grow;
    }

    /// <summary>
    /// Applies a Round's spreads to the candidates they reached: topped-up and revived quantities, and the spreads each spreader used up.
    /// </summary>
    /// <remarks>
    /// Creations are left to the write, since an instance spread creates is not a candidate until the next Round.
    /// </remarks>
    /// <param name="candidatesById">Every candidate, keyed by its <c>MapResourceInstanceId</c>.</param>
    internal static void ApplySpreadPlan(IReadOnlyDictionary<int, MapResourceRegenerationCandidate> candidatesById, MapResourceSpreadPlan spreadPlan)
    {
        foreach (SpreadTopUp topUp in spreadPlan.TopUps)
        {
            GetCandidate(candidatesById, topUp.MapResourceInstanceId).Quantity = topUp.Quantity;
        }

        foreach (SpreadRevival revival in spreadPlan.Revivals)
        {
            MapResourceRegenerationCandidate revived = GetCandidate(candidatesById, revival.MapResourceInstanceId);
            revived.Quantity = revival.Quantity;
            revived.SpreadLimit = revival.SpreadLimit;
            revived.DormantSinceRound = null;
            revived.IsRevivedBySpread = true;
        }

        foreach ((int spreaderId, int spreadsUsed) in spreadPlan.SpreadsUsed)
        {
            GetCandidate(candidatesById, spreaderId).SpreadLimit -= spreadsUsed;
        }
    }

    /// <summary>
    /// Grows a candidate classed <see cref="RegenerationCandidateType.Grow"/> by one step, from its quantity after any spread into it.
    /// </summary>
    internal static void ApplyGrowth(MapResourceRegenerationCandidate candidate)
    {
        RenewableHabitat habitat = candidate.Habitat;

        if (habitat.IsDistribution)
        {
            candidate.InstanceAmounts = GrowDistribution(candidate.InstanceAmounts, habitat.MaximumQuantity, habitat.GrowthPercentage);

            return;
        }

        candidate.Quantity = Grow(candidate.Quantity!.Value, habitat.MaximumQuantity, habitat.GrowthPercentage);
    }

    /// <summary>
    /// What becomes of a candidate classed <see cref="RegenerationCandidateType.Revive"/> that no spread revived.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>One just exhausted goes dormant, or is deleted at once when its type survives no Rounds. It first rolls the Round after.</description></item>
    ///   <item><description>A dormant one rolls to reseed each Round, and is deleted once it has failed in each of its type's <c>SurvivalRounds</c>.</description></item>
    ///   <item><description>A Distribution never reseeds: every amount is at 0, so there is nothing for a reseed to be shared between.</description></item>
    ///   <item><description>A Round no later than the one it went dormant in changes nothing, so a re-run of the Round neither rolls it nor ages it.</description></item>
    /// </list>
    /// </remarks>
    internal static RegrowthOutcome DecideRegrowth(MapResourceRegenerationCandidate candidate, long gameSeed, int currentRoundNumber)
    {
        MapResourceRegenerationInstance instance = candidate.Instance;

        if (instance.DormantSinceRound is not int dormantSinceRound)
        {
            return instance.SurvivalRounds == 0 ? RegrowthOutcome.Deleted : RegrowthOutcome.GoesDormant;
        }

        if (currentRoundNumber <= dormantSinceRound)
        {
            return RegrowthOutcome.StaysDormant;
        }

        if (candidate.Habitat.IsDistribution == false && RollsReseed(instance, gameSeed, currentRoundNumber))
        {
            return RegrowthOutcome.Reseeds;
        }

        return currentRoundNumber - dormantSinceRound >= instance.SurvivalRounds ? RegrowthOutcome.Deleted : RegrowthOutcome.StaysDormant;
    }

    /// <summary>
    /// Applies <see cref="DecideRegrowth"/> to a candidate.
    /// </summary>
    internal static void ApplyRegrowth(MapResourceRegenerationCandidate candidate, long gameSeed, int currentRoundNumber)
    {
        switch (DecideRegrowth(candidate, gameSeed, currentRoundNumber))
        {
            case RegrowthOutcome.GoesDormant:
                candidate.DormantSinceRound = currentRoundNumber;
                break;

            case RegrowthOutcome.Reseeds:
                candidate.Quantity = ReseedQuantity(candidate.Habitat);
                candidate.DormantSinceRound = null;
                break;

            case RegrowthOutcome.Deleted:
                candidate.IsDeleted = true;
                break;
        }
    }

    /// <summary>
    /// The quantity a reseeded instance comes back at: one step of growth on its ceiling, capped at that ceiling.
    /// </summary>
    internal static long ReseedQuantity(RenewableHabitat habitat)
    {
        return Math.Min(habitat.MaximumQuantity, GrowthAmount(habitat.MaximumQuantity, habitat.GrowthPercentage));
    }

    /// <summary>
    /// The rows a Round's regeneration writes: every candidate spread, growth or regrowth changed, and the instances spread created.
    /// </summary>
    internal static MapResourceRegenerationPlan BuildPlan(IReadOnlyCollection<MapResourceRegenerationCandidate> candidates, MapResourceSpreadPlan spreadPlan)
    {
        List<int> deletedIds = [];
        List<RegenerationInstanceUpdate> instanceUpdates = [];
        List<RegenerationAmountUpdate> amountUpdates = [];

        foreach (MapResourceRegenerationCandidate candidate in candidates)
        {
            MapResourceRegenerationInstance asRead = candidate.Instance;

            if (candidate.IsDeleted)
            {
                deletedIds.Add(asRead.MapResourceInstanceId);
                continue;
            }

            if (candidate.Quantity != asRead.Quantity || candidate.SpreadLimit != asRead.SpreadLimit || candidate.DormantSinceRound != asRead.DormantSinceRound)
            {
                instanceUpdates.Add(new RegenerationInstanceUpdate
                {
                    MapResourceInstanceId = asRead.MapResourceInstanceId,
                    Quantity = candidate.Quantity,
                    SpreadLimit = candidate.SpreadLimit,
                    DormantSinceRound = candidate.DormantSinceRound
                });
            }

            amountUpdates.AddRange(ChangedAmounts(candidate));
        }

        return new MapResourceRegenerationPlan
        {
            InstanceUpdates = instanceUpdates,
            AmountUpdates = amountUpdates,
            DeletedMapResourceInstanceIds = deletedIds,
            Creations = spreadPlan.Creations
        };
    }

    /// <summary>
    /// A Distribution candidate's amounts that differ from the amounts as read.
    /// </summary>
    private static IEnumerable<RegenerationAmountUpdate> ChangedAmounts(MapResourceRegenerationCandidate candidate)
    {
        if (ReferenceEquals(candidate.InstanceAmounts, candidate.Instance.InstanceAmounts))
        {
            return [];
        }

        Dictionary<int, long> amountsAsRead = candidate.Instance.InstanceAmounts.ToDictionary(amount => amount.AssetId, amount => amount.Amount);

        return candidate.InstanceAmounts
            .Where(amount => amountsAsRead.GetValueOrDefault(amount.AssetId) != amount.Amount)
            .Select(amount => new RegenerationAmountUpdate
            {
                MapResourceInstanceId = candidate.Instance.MapResourceInstanceId,
                AssetId = amount.AssetId,
                Amount = amount.Amount
            });
    }

    /// <summary>
    /// Whether a dormant instance comes back this Round, keyed on its id and the Round so each dormant Round draws afresh.
    /// </summary>
    private static bool RollsReseed(MapResourceRegenerationInstance instance, long gameSeed, int currentRoundNumber)
    {
        Random reseedRoll = GameRandom.Create(gameSeed, RandomPurpose.MapResourceReseed, [instance.MapResourceInstanceId, currentRoundNumber]);

        return (decimal)reseedRoll.NextDouble() < instance.ReseedLikelihood;
    }

    /// <summary>
    /// A candidate the spread plan names.
    /// </summary>
    /// <remarks>
    /// Every instance the plan names passed the same habitat check pairing did, from the same read, so a missing one is a defect in the process rather than in the data.
    /// </remarks>
    /// <param name="candidatesById">Every candidate, keyed by its <c>MapResourceInstanceId</c>.</param>
    private static MapResourceRegenerationCandidate GetCandidate(IReadOnlyDictionary<int, MapResourceRegenerationCandidate> candidatesById, int mapResourceInstanceId)
    {
        return candidatesById.TryGetValue(mapResourceInstanceId, out MapResourceRegenerationCandidate? candidate)
            ? candidate
            : throw new InvalidOperationException($"The spread plan names MapResourceInstance {mapResourceInstanceId}, which is not a regeneration candidate.");
    }
}
