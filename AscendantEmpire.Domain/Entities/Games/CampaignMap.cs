using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

[Table("CampaignMap", Schema = "game")]
public class CampaignMap
{
    [Key]
    public int CampaignMapId { get; set; }
    public required int GameId { get; set; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }

    public int? ForageAmount { get; set; }
    public int? HuntAmount { get; set; }
    public int? FishAmount { get; set; }
    public int? FodderAmount { get; set; }

    public int? FirstSeenOnTurnId { get; set; }
    public int? FirstSeenByPlayerId { get; set; }
    
    public int? LastVisitedOnTurnId { get; set; }
        /// <summary>
    /// Records the last Player Id who's Unit last visited this Tile.
    /// </summary>
    public int? LastVisitedByPlayerId { get; set; }

    public required BiomeType BiomeTypeId { get; set; }
    public required CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

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
}
