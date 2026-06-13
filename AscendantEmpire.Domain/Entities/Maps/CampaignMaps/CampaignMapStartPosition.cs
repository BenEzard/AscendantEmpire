using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

public class CampaignMapStartPosition
{
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    [Key]
    public int Id { get; set; }

    public int CampaignMapGalleryIndexId { get; set; }

    public int XCoord { get; set; }

    public int YCoord { get; set; }

    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public string? DisplayName { get; set; }

    public int? LocationScore { get; set; }

    public CampaignMapGalleryIndex CampaignMapGalleryIndex { get; set; } = null!;
}
