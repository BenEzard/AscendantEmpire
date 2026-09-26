using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

public class CampaignMap
{
    [Key]
    public int CampaignMapId { get; set; }
    public required int GameId { get; set; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }

    public int? FirstSeenOnTurnId { get; set; }
    public int? FirstSeenByPlayerId { get; set; }
    
    public int? LastVisitedOnTurnId { get; set; }
        /// <summary>
    /// Records the last Player Id who's Unit last visited this Tile.
    /// </summary>
    public int? LastVisitedByPlayerId { get; set; }

    public int? ControlledByPlayerId { get; set; }

    public required BiomeType BiomeTypeId { get; set; }
    public required CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    /// <summary>
    /// The river running along this tile's South edge — the edge it shares with the tile below it. Copied from the gallery map at game setup.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its North edge is the <see cref="SouthRiver"/> of the tile above it. See
    /// <see cref="CampaignMapGallery.SouthRiver"/>.
    /// </remarks>
    public RiverWidth SouthRiver { get; set; }

    /// <summary>
    /// The river running along this tile's East edge — the edge it shares with the tile to its right. Copied from the gallery map at game setup.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its West edge is the <see cref="EastRiver"/> of the tile to its left. See
    /// <see cref="CampaignMapGallery.EastRiver"/>.
    /// </remarks>
    public RiverWidth EastRiver { get; set; }

    [ForeignKey("BiomeTypeId")]
    public required CampaignBiome Biome { get; set; }

    [ForeignKey("CampaignMapGeographyTypeId")]
    public required CampaignGeography Geography { get; set; }

    [ForeignKey("GameId")]
    public required GameIndex Game { get; set; }

    [ForeignKey("FirstSeenByPlayerId")]
    public required Player? FirstVisitor { get; set; }

    [ForeignKey("LastVisitedByPlayerId")]
    public required Player? LastVisitor { get; set; }

    [ForeignKey("ControlledByPlayerId")]
    public required Player? ControlledBy { get; set; }
}
