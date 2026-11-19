using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

/// <summary>
/// One habitat of a renewable MapResourceType, and the values its instances regenerate by there.
/// </summary>
/// <remarks>
/// <para>
/// A null <see cref="CampaignMapGeographyTypeId"/> covers every Geography in the Biome, and a Geography-specific row beats it. Resolve which row a tile falls
/// under through <c>BiomeImpliedResourceRules.FindHabitat</c> rather than matching the Biome here.
/// </para>
/// <para>
/// A Distribution is renewable but holds its amount in child rows and never spreads, so <see cref="IsDistribution"/> is carried to keep it off the single-quantity path.
/// </para>
/// </remarks>
internal sealed record RenewableHabitat
{
    public int MapResourceTypeId { get; init; }
    public bool IsDistribution { get; init; }
    public BiomeType BiomeTypeId { get; init; }
    public CampaignMapGeographyType? CampaignMapGeographyTypeId { get; init; }
    public long MaximumQuantity { get; init; }
    public decimal GrowthPercentage { get; init; }
    public decimal? SpreadLikelihood { get; init; }
    public decimal? EstimationFuzzyFactorMin { get; init; }
    public decimal? EstimationFuzzyFactorMax { get; init; }
}

/// <summary>
/// The one step a renewable instance takes at the end of a Round, decided once from its quantity as read.
/// </summary>
internal enum RegenerationCandidateType
{
    /// <summary>
    /// At or above its ceiling. Rolls to spread if it can, and does not grow.
    /// </summary>
    Spread,

    /// <summary>
    /// Above 0 and below its ceiling.
    /// </summary>
    Grow,

    /// <summary>
    /// At 0. Goes dormant, rolls to reseed, or is deleted, unless spread revived it first.
    /// </summary>
    Revive
}

/// <summary>
/// A renewable instance paired with the habitat that covers its tile, the step it was classified into, and its state as spread, growth and regrowth leave it.
/// </summary>
/// <remarks>
/// <see cref="Instance"/> stays as read, since the classification and the spreads are decided from it. The working values start as copies of it, and the write compares
/// the two to find what changed.
/// </remarks>
internal sealed class MapResourceRegenerationCandidate(MapResourceRegenerationInstance instance, RenewableHabitat habitat, RegenerationCandidateType regenerationType)
{
    public MapResourceRegenerationInstance Instance { get; } = instance;
    public RenewableHabitat Habitat { get; } = habitat;
    public RegenerationCandidateType RegenerationType { get; } = regenerationType;

    public long? Quantity { get; set; } = instance.Quantity;
    public int? SpreadLimit { get; set; } = instance.SpreadLimit;
    public int? DormantSinceRound { get; set; } = instance.DormantSinceRound;

    /// <summary>
    /// A Distribution's amounts, replaced whole when it grows.
    /// </summary>
    public IReadOnlyCollection<DistributedQuantity> InstanceAmounts { get; set; } = instance.InstanceAmounts;

    /// <summary>
    /// Revived by a neighbour's spread this Round, so regrowth leaves it alone although it was classed <see cref="RegenerationCandidateType.Revive"/>.
    /// </summary>
    public bool IsRevivedBySpread { get; set; }

    public bool IsDeleted { get; set; }
}

/// <summary>
/// What becomes of an instance at 0 in this Round's regrowth.
/// </summary>
internal enum RegrowthOutcome
{
    /// <summary>
    /// Just exhausted: stamped dormant, and first rolls to reseed next Round.
    /// </summary>
    GoesDormant,

    /// <summary>
    /// Already dormant, and neither reseeded nor outlived its survival.
    /// </summary>
    StaysDormant,

    Reseeds,
    Deleted
}

/// <summary>
/// Every row a Round's regeneration writes, gathered so they land in one transaction.
/// </summary>
internal sealed record MapResourceRegenerationPlan
{
    public IReadOnlyCollection<RegenerationInstanceUpdate> InstanceUpdates { get; init; } = [];
    public IReadOnlyCollection<RegenerationAmountUpdate> AmountUpdates { get; init; } = [];
    public IReadOnlyCollection<int> DeletedMapResourceInstanceIds { get; init; } = [];
    public IReadOnlyCollection<SpreadCreation> Creations { get; init; } = [];
}

/// <summary>
/// An existing instance's columns after regeneration, written whole since any of them may have changed.
/// </summary>
internal sealed record RegenerationInstanceUpdate
{
    public int MapResourceInstanceId { get; init; }
    public long? Quantity { get; init; }
    public int? SpreadLimit { get; init; }
    public int? DormantSinceRound { get; init; }
}

/// <summary>
/// One of a Distribution's amounts after growth.
/// </summary>
internal sealed record RegenerationAmountUpdate
{
    public int MapResourceInstanceId { get; init; }
    public int AssetId { get; init; }
    public long Amount { get; init; }
}

/// <summary>
/// A tile a spread is aimed at: its terrain, and every MapResource already on it.
/// </summary>
internal sealed record SpreadTargetTile
{
    public int CampaignMapId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public BiomeType BiomeTypeId { get; init; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; init; }

    /// <summary>
    /// The tile's slots that hold a MapResource. An empty slot has no entry.
    /// </summary>
    public IReadOnlyCollection<SpreadTargetFilledSlot> FilledSlots { get; init; } = [];
}

/// <summary>
/// A MapResource already on a spread target.
/// </summary>
/// <remarks>
/// Every type is included, renewable or not, since every MapResource takes a slot a spread could otherwise fill.
/// </remarks>
internal sealed record SpreadTargetFilledSlot
{
    public int MapResourceInstanceId { get; init; }
    public int MapResourceTypeId { get; init; }
    public byte Slot { get; init; }
    public long? Quantity { get; init; }
}

/// <summary>
/// What a Round's spreads do, decided intent by intent against the targets as read.
/// </summary>
/// <remarks>
/// Applied to the working set before growth, never written by itself: the end of the Round writes one plan covering spread, growth and regrowth together.
/// </remarks>
internal sealed record MapResourceSpreadPlan
{
    /// <summary>
    /// Existing instances that spreads topped up, one entry each, holding the quantity after every spread into it this Round.
    /// </summary>
    public IReadOnlyCollection<SpreadTopUp> TopUps { get; init; } = [];

    /// <summary>
    /// Dormant instances that a spread revived, one entry each.
    /// </summary>
    /// <remarks>
    /// One revived and then spread into again this Round appears here only, with its final quantity, and not in <see cref="TopUps"/>.
    /// </remarks>
    public IReadOnlyCollection<SpreadRevival> Revivals { get; init; } = [];

    /// <summary>
    /// Instances spread created, each holding its quantity after any later spread into it this Round.
    /// </summary>
    public IReadOnlyCollection<SpreadCreation> Creations { get; init; } = [];

    /// <summary>
    /// How many spreads each spreading instance used up, keyed by its <c>MapResourceInstanceId</c>.
    /// </summary>
    public IReadOnlyDictionary<int, int> SpreadsUsed { get; init; } = new Dictionary<int, int>();
}

/// <summary>
/// An existing instance's quantity after the spreads into it this Round.
/// </summary>
internal sealed record SpreadTopUp
{
    public int MapResourceInstanceId { get; init; }
    public long Quantity { get; init; }
}

/// <summary>
/// A dormant instance brought back by spread, which clears its dormancy.
/// </summary>
internal sealed record SpreadRevival
{
    public int MapResourceInstanceId { get; init; }
    public long Quantity { get; init; }

    /// <summary>
    /// The reviving instance's <c>SpreadLimit</c> after the spread was used up.
    /// </summary>
    public int SpreadLimit { get; init; }
}

/// <summary>
/// A MapResource spread creates on a tile that had none of its type.
/// </summary>
internal sealed record SpreadCreation
{
    public int CampaignMapId { get; init; }

    /// <summary>
    /// The tile's coordinates, which the new instance's creation rolls key on along with its <see cref="Slot"/>.
    /// </summary>
    public int XCoord { get; init; }

    public int YCoord { get; init; }
    public byte Slot { get; init; }
    public int MapResourceTypeId { get; init; }
    public long Quantity { get; init; }

    /// <summary>
    /// The spreading instance's <c>SpreadLimit</c> after the spread was used up.
    /// </summary>
    public int SpreadLimit { get; init; }

    public decimal? EstimationFuzzyFactor { get; init; }

    /// <summary>
    /// The instance that spread, whose discoverers discover this one too.
    /// </summary>
    public int SourceMapResourceInstanceId { get; init; }
}
