using AscendantEmpire.Domain.Enums.Games;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

[Table("Index", Schema = "game")]
public class GameIndex
{
    public const int GAME_NAME_MAX_LENGTH = 250;

    [Key]
    public int GameIndexId { get; set; }
    public int CampaignMapIndexId { get; set; }
    [MaxLength(GAME_NAME_MAX_LENGTH)]
    public required string GameName { get; set; }
    public required int NumberOfPlayers { get; set; }
    public Guid GameGuid { get; set; }
    public required GameCreationStatus CreationStatus { get; set; }
    public required GamePlayStatus PlayStatus { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}

