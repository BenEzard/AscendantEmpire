using AscendantEmpire.Domain.Enums.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Games;

public class Player
{
    public const int RULER_TITLE_MAX_LENGTH = 50;
    public const int RULER_NAME_MAX_LENGTH = 50;
    public const int FACTION_NAME_MAX_LENGTH = 50;

    [Key]
    public int Id { get; set; }
    public int GameId { get; set; }

    /// <summary>
    /// A User can be null when the Player slot is unassigned.
    /// </summary>
    public int? UserId { get; set; }

    [MaxLength(RULER_TITLE_MAX_LENGTH)]
    public required string RulerTitle { get; set; }
    
    [MaxLength(RULER_NAME_MAX_LENGTH)]
    public required string RulerName { get; set; }
    
    [MaxLength(FACTION_NAME_MAX_LENGTH)]
    public required string FactionName { get; set; }
    public PlayerStatus Status { get; set; }
    public int CampaignOffsetId { get; set; }

    public GameIndex Game { get; set; } = null!;
}
