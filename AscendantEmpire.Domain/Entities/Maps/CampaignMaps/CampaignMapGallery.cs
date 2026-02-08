using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

public class CampaignMapGallery
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int CampaignMapGalleryId { get; set; }
    public required int CampaignMapGalleryIndexId { get; set; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }

    public required BiomeType BiomeTypeId { get; set; }
    public required CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    [ForeignKey("BiomeTypeId")]
    public required CampaignBiome Biome { get; set; }

    [ForeignKey("CampaignMapGeographyTypeId")]
    public required CampaignGeography Geography { get; set; }

    public required CampaignMapGalleryIndex Index { get; set; }
}
