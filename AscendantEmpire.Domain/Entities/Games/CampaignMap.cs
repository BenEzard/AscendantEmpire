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

    public int? FirstVisitedTurnId { get; set; }
    public int? FirstVisitedPlayerId { get; set; }
    public int? LastVisitedTurnId { get; set; }
    public int? LastVisitedPlayerId { get; set; }

    public required BiomeType BiomeTypeId { get; set; }
    public required CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    [ForeignKey("BiomeTypeId")]
    public required CampaignBiome Biome { get; set; }

    [ForeignKey("CampaignMapGeographyTypeId")]
    public required CampaignGeography Geography { get; set; }

    [ForeignKey("GameId")]
    public required GameIndex Game { get; set; }

    [ForeignKey("FirstVisitedPlayerId")]
    public required Player? FirstVisitor { get; set; }

    [ForeignKey("LastVisitedPlayerId")]
    public required Player? LastVisitor { get; set; }
}
