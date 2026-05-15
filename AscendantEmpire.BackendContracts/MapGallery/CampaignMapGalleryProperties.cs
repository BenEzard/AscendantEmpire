using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.MapGallery;

public sealed record CampaignMapGalleryProperties
{
    public required int Id { get; init; }
    public required string MapName { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required MapCreationStatus CreationStatus { get; init; }
    public required int CampaignMapOffsetId { get; init; }
    public required bool HorizontalWrap { get; init; }
    public required bool VerticalWrap { get; init; }
}
