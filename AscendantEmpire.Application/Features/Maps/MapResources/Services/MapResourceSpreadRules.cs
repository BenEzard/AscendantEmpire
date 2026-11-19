using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Helpers.Randomness;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

internal static class MapResourceSpreadRules
{
    /// <summary>
    /// The directions a spread can go, in the order the direction roll indexes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is part of every seeded spread result, like a <see cref="RandomPurpose"/> number: reordering it changes where existing Games spread.
    /// </para>
    /// <para>
    /// Every direction is listed, including ones a tile's position blocks, so a patch with fewer compatible neighbours spreads less often.
    /// </para>
    /// </remarks>
    private static readonly MoveDirection[] _SPREAD_DIRECTIONS =
    [
        MoveDirection.North,
        MoveDirection.NorthEast,
        MoveDirection.East,
        MoveDirection.SouthEast,
        MoveDirection.South,
        MoveDirection.SouthWest,
        MoveDirection.West,
        MoveDirection.NorthWest
    ];

    internal static SpreadTargetResults SelectSpreadTargets(
        CampaignMapGeometry mapGeometry,
        IReadOnlyCollection<MapResourceRegenerationCandidate> candidates,
        long gameSeed,
        int currentRoundNumber)
    {
        // A list of neighbours selected for MapResource spread. (Still requires final checks to make sure the placement is valid)
        List<SpreadIntent> potentialSpreadNeighbours = [];
        foreach (MapResourceRegenerationCandidate candidate in candidates)
        {
            if (CanSpread(candidate) == false)
            {
                continue;
            }

            Random spreadRoll = GameRandom.Create(
                gameSeed: gameSeed, 
                purpose: RandomPurpose.MapResourceSpread, 
                discriminators: [candidate.Instance.MapResourceInstanceId, currentRoundNumber]);

            if ((decimal)spreadRoll.NextDouble() >= candidate.Habitat.SpreadLikelihood!.Value)
            {
                // Spread doesn't occur for this instance this round, skip it.
                continue;
            }

            Random directionRoll = GameRandom.Create(
                gameSeed, 
                purpose: RandomPurpose.MapResourceSpreadDirection,
                discriminators: [candidate.Instance.MapResourceInstanceId, currentRoundNumber]);


            MoveDirection direction = _SPREAD_DIRECTIONS[directionRoll.Next(_SPREAD_DIRECTIONS.Length)];
            Coord origin = new(candidate.Instance.XCoord, candidate.Instance.YCoord);

            // Off a non-wrapping edge is a blocked direction: no spread, and none used up.
            if (MapDirections.TryGetNeighbour(origin, 
                direction, 
                mapGeometry.Width, 
                mapGeometry.Height, 
                mapGeometry.WrapsHorizontally, 
                mapGeometry.WrapsVertically, 
                out Coord? target) == false)
            {
                continue;
            }

            // Build up the list of potential spread neighbours, which will be checked for validity later.
            potentialSpreadNeighbours.Add(new SpreadIntent
            {
                Candidate = candidate,
                TargetCampaignMapId = target!.ToEnhancedCoord(mapGeometry.CampaignMapOffsetId, mapGeometry.Width).DbId
            });
        }

        return new SpreadTargetResults
        {
            SpreadIntent = potentialSpreadNeighbours,
            TargetCampaignMapIds = [.. potentialSpreadNeighbours.Select(intent => intent.TargetCampaignMapId).Distinct()]
        };
    }

    /// <summary>
    /// Check that this candidate can spread.
    /// </summary>
    private static bool CanSpread(MapResourceRegenerationCandidate candidate)
    {
        return candidate is
        {
            RegenerationType: RegenerationCandidateType.Spread,
            Habitat.SpreadLikelihood: > 0,
            Instance.SpreadLimit: > 0
        };
    }

    /// <summary>
    /// Decides what each spread does to its target tile, and gathers the results into one plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates and applies in one pass, against each target's slots as the spreads ahead have left them. Whether a spread is valid depends on those spreads: two into one empty
    /// tile become a creation and a top-up, and a slot filled by an earlier spread is not free for a later one.
    /// </para>
    /// <para>
    /// Intents are walked in order of the spreading instance's id, so the plan does not depend on the order they arrive in. A target's ceiling is its habitat's;
    /// an owner's modifiers are not applied.
    /// </para>
    /// </remarks>
    /// <param name="spreadTargets">Each targeted tile as read, keyed by its <c>CampaignMapId</c>. An intent whose tile is missing targets void.</param>
    /// <param name="habitatsByType">Each renewable type's habitats keyed by scope, themselves keyed by <c>MapResourceTypeId</c>.</param>
    internal static MapResourceSpreadPlan Plan(
        IReadOnlyCollection<SpreadIntent> spreadIntents,
        IReadOnlyDictionary<int, SpreadTargetTile> spreadTargets,
        IReadOnlyDictionary<int, Dictionary<HabitatScope, RenewableHabitat>> habitatsByType,
        long gameSeed)
    {
        Dictionary<int, List<SpreadTargetSlot>> slotsByTile = [];
        Dictionary<int, int> spreadsUsed = [];

        foreach (SpreadIntent intent in spreadIntents.OrderBy(intent => intent.Candidate.Instance.MapResourceInstanceId))
        {
            MapResourceRegenerationInstance spreader = intent.Candidate.Instance;

            // A tile the read did not return is void.
            if (spreadTargets.TryGetValue(intent.TargetCampaignMapId, out SpreadTargetTile? targetTile) == false)
            {
                continue;
            }

            RenewableHabitat? targetHabitat = MapResourceRegenerationRules.FindHabitat(
                habitatsByType,
                spreader.MapResourceTypeId,
                targetTile.BiomeTypeId,
                targetTile.CampaignMapGeographyTypeId);

            if (targetHabitat == null)
            {
                continue;
            }

            List<SpreadTargetSlot> slots = GetSpreadTargetSlots(slotsByTile, targetTile);
            SpreadTargetSlot? sameType = slots.SingleOrDefault(slot => slot.MapResourceTypeId == spreader.MapResourceTypeId);
            long targetCeiling = targetHabitat.MaximumQuantity;
            int spreadLimitAfter = spreader.SpreadLimit!.Value - 1;

            // A new or revived instance starts at the spreader's growth share of its own quantity, capped at the target's ceiling.
            long startingQuantity = Math.Min(
                targetCeiling,
                MapResourceRegenerationRules.GrowthAmount(spreader.Quantity!.Value, intent.Candidate.Habitat.GrowthPercentage));

            if (sameType == null)
            {
                byte? freeSlot = FindFreeSlot(slots);

                if (freeSlot == null)
                {
                    continue;
                }

                slots.Add(new SpreadTargetSlot
                {
                    CampaignMapId = targetTile.CampaignMapId,
                    MapResourceInstanceId = null,
                    MapResourceTypeId = spreader.MapResourceTypeId,
                    Slot = freeSlot.Value,
                    XCoord = targetTile.XCoord,
                    YCoord = targetTile.YCoord,
                    Quantity = startingQuantity,
                    SpreadLimit = spreadLimitAfter,
                    SourceMapResourceInstanceId = spreader.MapResourceInstanceId,
                    EstimationFuzzyFactor = MapResourceInstantiationRules.RollEstimationFuzzyFactor(
                        gameSeed,
                        targetTile.XCoord,
                        targetTile.YCoord,
                        freeSlot.Value,
                        targetHabitat.EstimationFuzzyFactorMin,
                        targetHabitat.EstimationFuzzyFactorMax)
                });
            }
            else if (sameType.Quantity == 0)
            {
                sameType.Quantity = startingQuantity;
                sameType.IsRevived = true;
                sameType.SpreadLimit = spreadLimitAfter;
            }
            else if (sameType.Quantity >= targetCeiling)
            {
                continue;
            }
            else
            {
                sameType.Quantity = MapResourceRegenerationRules.Grow(sameType.Quantity, targetCeiling, targetHabitat.GrowthPercentage);
                sameType.IsToppedUp = true;
            }

            spreadsUsed[spreader.MapResourceInstanceId] = spreadsUsed.GetValueOrDefault(spreader.MapResourceInstanceId) + 1;
        }

        return BuildPlan(slotsByTile.Values.SelectMany(slots => slots), spreadsUsed);
    }

    /// <summary>
    /// A target's filled slots, made from the tile as read the first time a spread reaches it.
    /// </summary>
    /// <param name="slotsByTile">Each target's filled slots as spread has left them so far, keyed by the tile's <c>CampaignMapId</c>. Added to on a tile's first visit.</param>
    private static List<SpreadTargetSlot> GetSpreadTargetSlots(Dictionary<int, List<SpreadTargetSlot>> slotsByTile, SpreadTargetTile targetTile)
    {
        if (slotsByTile.TryGetValue(targetTile.CampaignMapId, out List<SpreadTargetSlot>? slots))
        {
            return slots;
        }

        // A quantity held in child rows reads as 0 here, which only matters for the spreading type, and a Distribution never spreads.
        slots =
        [
            .. targetTile.FilledSlots.Select(filledSlot => new SpreadTargetSlot
            {
                CampaignMapId = targetTile.CampaignMapId,
                MapResourceInstanceId = filledSlot.MapResourceInstanceId,
                MapResourceTypeId = filledSlot.MapResourceTypeId,
                Slot = filledSlot.Slot,
                Quantity = filledSlot.Quantity ?? 0
            })
        ];

        slotsByTile.Add(targetTile.CampaignMapId, slots);

        return slots;
    }

    /// <summary>
    /// The lowest slot number on the tile that nothing holds, or <see langword="null"/> when it is full.
    /// </summary>
    private static byte? FindFreeSlot(List<SpreadTargetSlot> slots)
    {
        HashSet<byte> takenSlotNumbers = [.. slots.Select(slot => slot.Slot)];

        for (byte slotNumber = MapResourceInstance.MIN_SLOT; slotNumber <= MapResourceInstance.MAX_SLOT; slotNumber++)
        {
            if (takenSlotNumbers.Contains(slotNumber) == false)
            {
                return slotNumber;
            }
        }

        return null;
    }

    /// <summary>
    /// Sorts the slots that spread changed into the plan's creations, revivals and top-ups.
    /// </summary>
    /// <remarks>
    /// One <c>if</c> chain rather than a query per list, because it is what keeps the lists apart: a slot revived and then topped up belongs in the revivals only.
    /// </remarks>
    /// <param name="slots">Every target's filled slots once every spread has been applied.</param>
    /// <param name="spreadsUsed">How many spreads each spreading instance used up, keyed by its <c>MapResourceInstanceId</c>.</param>
    private static MapResourceSpreadPlan BuildPlan(IEnumerable<SpreadTargetSlot> slots, Dictionary<int, int> spreadsUsed)
    {
        List<SpreadCreation> creations = [];
        List<SpreadRevival> revivals = [];
        List<SpreadTopUp> topUps = [];

        foreach (SpreadTargetSlot slot in slots)
        {
            if (slot.MapResourceInstanceId == null)
            {
                creations.Add(new SpreadCreation
                {
                    CampaignMapId = slot.CampaignMapId,
                    XCoord = slot.XCoord,
                    YCoord = slot.YCoord,
                    Slot = slot.Slot,
                    MapResourceTypeId = slot.MapResourceTypeId,
                    Quantity = slot.Quantity,
                    SpreadLimit = slot.SpreadLimit,
                    EstimationFuzzyFactor = slot.EstimationFuzzyFactor,
                    SourceMapResourceInstanceId = slot.SourceMapResourceInstanceId
                });
            }
            else if (slot.IsRevived)
            {
                revivals.Add(new SpreadRevival
                {
                    MapResourceInstanceId = slot.MapResourceInstanceId.Value,
                    Quantity = slot.Quantity,
                    SpreadLimit = slot.SpreadLimit
                });
            }
            else if (slot.IsToppedUp)
            {
                topUps.Add(new SpreadTopUp
                {
                    MapResourceInstanceId = slot.MapResourceInstanceId.Value,
                    Quantity = slot.Quantity
                });
            }
        }

        return new MapResourceSpreadPlan
        {
            Creations = creations,
            Revivals = revivals,
            TopUps = topUps,
            SpreadsUsed = spreadsUsed
        };
    }

    /// <summary>
    /// One filled slot on a target tile, holding what spread has left there so far this Round, whether it was there already or spread created it.
    /// </summary>
    private sealed class SpreadTargetSlot
    {
        public int CampaignMapId { get; init; }

        /// <summary>
        /// The instance's id, or <see langword="null"/> for one spread created this Round, which has none until it is inserted.
        /// </summary>
        public int? MapResourceInstanceId { get; init; }

        public int MapResourceTypeId { get; init; }
        public byte Slot { get; init; }
        public long Quantity { get; set; }
        public bool IsRevived { get; set; }
        public bool IsToppedUp { get; set; }

        /// <summary>
        /// The spread limit inherited from the spreader. Set only on an instance spread created or revived.
        /// </summary>
        public int SpreadLimit { get; set; }

        public int XCoord { get; init; }
        public int YCoord { get; init; }
        public decimal? EstimationFuzzyFactor { get; init; }
        public int SourceMapResourceInstanceId { get; init; }
    }
}

/// <summary>
/// The results of the first part of MapResource Spreading.
/// </summary>
/// <remarks>Contains a list of *potential* spread targets, which have <b>NOT</b> been validated as viable spread sites.</remarks>
internal sealed record SpreadTargetResults
{
    /// <summary>A list of neighbours selected for MapResource spread. (Still requires final checks to make sure the placement is valid)</summary>
    /// <remarks>
    /// Two intents can share a target. Both are kept: each is its own spread, so the second tops up what the first created, and each uses up its own spreader's <c>SpreadLimit</c>.
    /// </remarks>
    public IReadOnlyCollection<SpreadIntent> SpreadIntent { get; init; } = [];

    /// <summary>
    /// Each tile the intents target, once, for reading their terrain and the MapResources already on them.
    /// </summary>
    public IReadOnlyCollection<int> TargetCampaignMapIds { get; init; } = [];
}

internal sealed record SpreadIntent
{
    public required MapResourceRegenerationCandidate Candidate { get; init; }
    public required int TargetCampaignMapId { get; init; }
}
