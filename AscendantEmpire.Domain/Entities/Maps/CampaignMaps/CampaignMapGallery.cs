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

    /// <summary>
    /// The river running along this tile's South edge — the edge it shares with the tile below it.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its North edge is the <see cref="SouthRiver"/> of the tile above it. On an axis that does not wrap,
    /// the bottom row's South edge faces off the map and must be <see cref="RiverWidth.None"/>.
    /// </remarks>
    public RiverWidth SouthRiver { get; set; }

    /// <summary>
    /// The river running along this tile's East edge — the edge it shares with the tile to its right.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its West edge is the <see cref="EastRiver"/> of the tile to its left. On an axis that does not wrap,
    /// the last column's East edge faces off the map and must be <see cref="RiverWidth.None"/>.
    /// </remarks>
    public RiverWidth EastRiver { get; set; }

    [ForeignKey("BiomeTypeId")]
    public required CampaignBiome Biome { get; set; }

    [ForeignKey("CampaignMapGeographyTypeId")]
    public required CampaignGeography Geography { get; set; }

    public required CampaignMapGalleryIndex Index { get; set; }
}
