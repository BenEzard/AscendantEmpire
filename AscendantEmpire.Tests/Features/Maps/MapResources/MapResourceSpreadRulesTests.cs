using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins which renewable MapResources spread at the end of a Round, and what each spread does to the tile it reaches.
/// </summary>
/// <remarks>
/// No database: <see cref="MapResourceSpreadRules"/> is pure, and the tiles a spread reaches are handed to it as read.
/// </remarks>
[TestFixture]
internal sealed class MapResourceSpreadRulesTests
{
    private const long _SEED = 20261119;
    private const int _ROUND = 12;
    private const int _BERRY_TYPE_ID = 11;
    private const int _DEER_TYPE_ID = 12;
    private const int _COAL_TYPE_ID = 13;
    private const long _CEILING = 10_000_000;
    private const decimal _GROWTH_PERCENTAGE = 0.1m;
    private const int _SPREAD_LIMIT = 3;
    private const int _TARGET_TILE_ID = 900;

    /// <summary>
    /// What a spread from a spreader at the ceiling starts a new or revived instance at: its growth share.
    /// </summary>
    private const long _STARTING_QUANTITY = 1_000_000;

    private readonly Dictionary<int, Dictionary<HabitatScope, RenewableHabitat>> _habitatsByType = new()
    {
        [_BERRY_TYPE_ID] = new Dictionary<HabitatScope, RenewableHabitat> { [new HabitatScope(BiomeType.Forest, null)] = BerryHabitat() }
    };

    #region Selecting targets
    [Test, Description("A direction off an unwrapped edge is blocked rather than redirected, so a corner patch only ever spreads to its on-map neighbours, and less often than one in the open.")]
    public void ShouldOnlyTargetOnMapNeighboursWhenTheSpreaderIsInAnUnwrappedCorner()
    {
        CampaignMapGeometry geometry = new(CampaignMapOffsetId: 0, Width: 5, Height: 5, WrapsHorizontally: false, WrapsVertically: false);
        MapResourceRegenerationCandidate[] spreaders = [.. Enumerable.Range(1, 60).Select(id => Spreader(id, spreadLikelihood: 1m))];

        SpreadTargetResults results = MapResourceSpreadRules.SelectSpreadTargets(geometry, spreaders, _SEED, _ROUND);

        using (Assert.EnterMultipleScope())
        {
            // East, south-east and south of (0, 0), as row-major tile ids on a 5-wide map.
            Assert.That(results.SpreadIntent.Select(intent => intent.TargetCampaignMapId), Is.All.AnyOf(1, 5, 6));
            Assert.That(results.SpreadIntent, Has.Count.LessThan(spreaders.Length));
        }
    }

    [Test, Description("A spreader with no spreads left never spreads, however certain its roll.")]
    public void ShouldNotSpreadWhenTheSpreaderHasNoSpreadsLeft()
    {
        CampaignMapGeometry geometry = new(CampaignMapOffsetId: 0, Width: 5, Height: 5, WrapsHorizontally: true, WrapsVertically: true);

        SpreadTargetResults results = MapResourceSpreadRules.SelectSpreadTargets(geometry, [Spreader(1, spreadLikelihood: 1m, spreadLimit: 0)], _SEED, _ROUND);

        Assert.That(results.SpreadIntent, Is.Empty);
    }
    #endregion

    #region Planning
    [Test, Description("A spread into a compatible tile with a free slot creates an instance there, inheriting the spreader's limit after the spread is used up.")]
    public void ShouldCreateAnInstanceWhenTheTargetHasAFreeSlot()
    {
        MapResourceRegenerationCandidate spreader = Spreader(1);

        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(spreader)], Targets(Tile()), _habitatsByType, _SEED);

        SpreadCreation creation = plan.Creations.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(creation.CampaignMapId, Is.EqualTo(_TARGET_TILE_ID));
            Assert.That(creation.Slot, Is.EqualTo(1));
            Assert.That(creation.Quantity, Is.EqualTo(_STARTING_QUANTITY));
            Assert.That(creation.SpreadLimit, Is.EqualTo(_SPREAD_LIMIT - 1));
            Assert.That(creation.SourceMapResourceInstanceId, Is.EqualTo(spreader.Instance.MapResourceInstanceId));
            Assert.That(plan.SpreadsUsed[spreader.Instance.MapResourceInstanceId], Is.EqualTo(1));
        }
    }

    [Test, Description("Another type blocks a spread only by taking a slot, so the new instance takes the slot left free.")]
    public void ShouldCreateInTheFreeSlotWhenAnotherTypeHoldsTheFirst()
    {
        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(1))], Targets(Tile(Filled(_DEER_TYPE_ID, slot: 1, quantity: 5_000))), _habitatsByType, _SEED);

        Assert.That(plan.Creations.Single().Slot, Is.EqualTo(2));
    }

    [Test, Description("Two spreads into one empty tile make one instance that the second tops up, since a tile holds at most one of each type.")]
    public void ShouldTopUpWhatAnEarlierSpreadCreatedWhenTwoSpreadsShareATarget()
    {
        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(2)), Intent(Spreader(1))], Targets(Tile()), _habitatsByType, _SEED);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.Creations.Single().Quantity, Is.EqualTo(MapResourceRegenerationRules.Grow(_STARTING_QUANTITY, _CEILING, _GROWTH_PERCENTAGE)));
            Assert.That(plan.Creations.Single().SourceMapResourceInstanceId, Is.EqualTo(1), "Intents are walked in spreader id order, so the lower id creates.");
            Assert.That(plan.TopUps, Is.Empty);
            Assert.That(plan.SpreadsUsed.Keys, Is.EquivalentTo(new[] { 1, 2 }));
        }
    }

    [Test, Description("A spread into the same type below its ceiling grows it by the target's own growth percentage.")]
    public void ShouldTopUpTheSameTypeWhenItIsBelowItsCeiling()
    {
        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(1))], Targets(Tile(Filled(_BERRY_TYPE_ID, slot: 1, quantity: 2_000_000))), _habitatsByType, _SEED);

        Assert.That(plan.TopUps.Single().Quantity, Is.EqualTo(2_200_000));
    }

    [Test, Description("A dormant instance of the same type is revived as a new instance would be, inheriting the spreader's limit.")]
    public void ShouldReviveTheSameTypeWhenItIsDormant()
    {
        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(1))], Targets(Tile(Filled(_BERRY_TYPE_ID, slot: 2, quantity: 0))), _habitatsByType, _SEED);

        SpreadRevival revival = plan.Revivals.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(revival.Quantity, Is.EqualTo(_STARTING_QUANTITY));
            Assert.That(revival.SpreadLimit, Is.EqualTo(_SPREAD_LIMIT - 1));
            Assert.That(plan.TopUps, Is.Empty);
        }
    }

    [Test, Description("The same type at its ceiling has no room, so the spread fails and uses up nothing.")]
    public void ShouldRefuseWhenTheSameTypeIsAtItsCeiling()
    {
        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(1))], Targets(Tile(Filled(_BERRY_TYPE_ID, slot: 1, quantity: _CEILING))), _habitatsByType, _SEED);

        AssertNothingSpread(plan);
    }

    [Test, Description("A tile with both slots taken by other types has nowhere to put a new instance, and a dormant one still holds its slot.")]
    public void ShouldRefuseWhenBothSlotsAreTaken()
    {
        SpreadTargetTile fullTile = Tile(Filled(_DEER_TYPE_ID, slot: 1, quantity: 0), Filled(_COAL_TYPE_ID, slot: 2, quantity: null));

        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(1))], Targets(fullTile), _habitatsByType, _SEED);

        AssertNothingSpread(plan);
    }

    [Test, Description("A tile whose terrain the type has no habitat on is non-compatible, so nothing grows there and no spread is used up.")]
    public void ShouldRefuseWhenTheTargetHasNoHabitatForTheType()
    {
        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(1))], Targets(Tile() with { BiomeTypeId = BiomeType.Desert }), _habitatsByType, _SEED);

        AssertNothingSpread(plan);
    }

    [Test, Description("A target the read did not return is void, which is non-compatible.")]
    public void ShouldRefuseWhenTheTargetTileWasNotRead()
    {
        MapResourceSpreadPlan plan = MapResourceSpreadRules.Plan([Intent(Spreader(1))], new Dictionary<int, SpreadTargetTile>(), _habitatsByType, _SEED);

        AssertNothingSpread(plan);
    }
    #endregion

    /// <summary>
    /// A refused spread changes no tile and uses up nothing.
    /// </summary>
    private static void AssertNothingSpread(MapResourceSpreadPlan plan)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.Creations, Is.Empty);
            Assert.That(plan.TopUps, Is.Empty);
            Assert.That(plan.Revivals, Is.Empty);
            Assert.That(plan.SpreadsUsed, Is.Empty);
        }
    }

    private static RenewableHabitat BerryHabitat(decimal spreadLikelihood = 0.5m)
    {
        return new RenewableHabitat
        {
            MapResourceTypeId = _BERRY_TYPE_ID,
            BiomeTypeId = BiomeType.Forest,
            MaximumQuantity = _CEILING,
            GrowthPercentage = _GROWTH_PERCENTAGE,
            SpreadLikelihood = spreadLikelihood
        };
    }

    /// <summary>
    /// A Berry at its ceiling on the map's corner tile, classed to spread.
    /// </summary>
    private static MapResourceRegenerationCandidate Spreader(int mapResourceInstanceId, decimal spreadLikelihood = 0.5m, int spreadLimit = _SPREAD_LIMIT)
    {
        MapResourceRegenerationInstance instance = new()
        {
            MapResourceInstanceId = mapResourceInstanceId,
            MapResourceTypeId = _BERRY_TYPE_ID,
            BiomeTypeId = BiomeType.Forest,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Quantity = _CEILING,
            SpreadLimit = spreadLimit,
            SurvivalRounds = 3,
            ReseedLikelihood = 0.5m
        };

        return new MapResourceRegenerationCandidate(instance, BerryHabitat(spreadLikelihood), RegenerationCandidateType.Spread);
    }

    private static SpreadIntent Intent(MapResourceRegenerationCandidate spreader)
    {
        return new SpreadIntent { Candidate = spreader, TargetCampaignMapId = _TARGET_TILE_ID };
    }

    private static SpreadTargetTile Tile(params SpreadTargetFilledSlot[] filledSlots)
    {
        return new SpreadTargetTile
        {
            CampaignMapId = _TARGET_TILE_ID,
            XCoord = 1,
            YCoord = 1,
            BiomeTypeId = BiomeType.Forest,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            FilledSlots = filledSlots
        };
    }

    private static SpreadTargetFilledSlot Filled(int mapResourceTypeId, byte slot, long? quantity)
    {
        return new SpreadTargetFilledSlot { MapResourceInstanceId = 700 + slot, MapResourceTypeId = mapResourceTypeId, Slot = slot, Quantity = quantity };
    }

    private static Dictionary<int, SpreadTargetTile> Targets(SpreadTargetTile tile)
    {
        return new Dictionary<int, SpreadTargetTile> { [tile.CampaignMapId] = tile };
    }
}
