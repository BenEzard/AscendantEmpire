using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

[Table("PlayerCampaignMap", Schema = "game")]
public class PlayerCampaignMap
{
    [Key]
    public int PlayerCampaignMapId { get; set; }
    public required int GameId { get; set; }
    public int CampaignMapId { get; set; }
    public required int PlayerId { get; set; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }

    public required BiomeType BiomeTypeId { get; set; }
    public required CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    public int? FirstVisitedTurnId { get; set; }
    public int? LastVisitedTurnId { get; set; }

    [ForeignKey("CampaignMapId")]
    public required CampaignMap CampaignMap { get; set; }

    [ForeignKey("BiomeTypeId")]
    public required CampaignBiome Biome { get; set; }

    [ForeignKey("CampaignMapGeographyTypeId")]
    public required CampaignGeography Geography { get; set; }

    [ForeignKey("GameId")]
    public required GameIndex Game { get; set; }

    [ForeignKey("PlayerId")]
    public required Player Player { get; set; }
}
