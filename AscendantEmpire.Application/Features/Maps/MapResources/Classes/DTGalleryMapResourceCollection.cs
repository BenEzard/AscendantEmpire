using AscendantEmpire.FrontendContracts.MapResources.GalleryPlacements;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

/// <remarks>
/// Carries the handler's <c>GalleryMapResource</c> records as they are, since they already implement <see cref="IGalleryMapResource"/>.
/// </remarks>
public class DTGalleryMapResourceCollection : IGalleryMapResourceCollection
{
    public required int CampaignMapGalleryIndexId { get; set; }

    public required IReadOnlyCollection<IGalleryMapResource> MapResources { get; set; }
}
