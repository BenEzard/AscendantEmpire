using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

/// <summary>
/// A renewable MapResource instance, with the tile it stands on, as the end of a Round regenerates it.
/// </summary>
/// <remarks>
/// A Distribution holds its amount in <see cref="InstanceAmounts"/> rather than <see cref="Quantity"/>, and never spreads.
/// </remarks>
internal sealed record MapResourceRegenerationInstance
{
    public int MapResourceInstanceId { get; init; }
    public int MapResourceTypeId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public BiomeType BiomeTypeId { get; init; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; init; }
    public long? Quantity { get; init; }
    public int? SpreadLimit { get; init; }
    public int? DormantSinceRound { get; init; }
    public int SurvivalRounds { get; init; }
    public decimal ReseedLikelihood { get; init; }
    public IReadOnlyCollection<DistributedQuantity> InstanceAmounts { get; init; } = [];
}

/// <summary>
/// One Asset and its quantity, for a MapResource instance that is a Distribution.
/// </summary>
internal sealed record DistributedQuantity
{
    public int AssetId { get; init; }
    public long Amount { get; init; }
}