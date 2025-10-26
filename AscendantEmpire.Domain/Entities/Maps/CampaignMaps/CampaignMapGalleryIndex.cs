using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

[Table("CampaignMapIndex", Schema = "mapgallery")]
public class CampaignMapGalleryIndex
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string MapName { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }
    public MapCreationStatus CreationStatus { get; set; }
    public MapCreationEngine CreationEngine { get; set; }
    public required string Status { get; set; }
    public required string? ErrorMessage { get; set; }
    public required string PropertiesJson { get; set; }
    public int CampaignMapOffsetId { get; set; }
}
