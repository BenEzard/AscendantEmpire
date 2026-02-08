using AscendantEmpire.Domain.Enums.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Games;

public class GameIndex
{
    public const int GAME_NAME_MAX_LENGTH = 250;

    [Key]
    public int Id { get; set; }
    public int CampaignMapIndexId { get; set; }
    [MaxLength(GAME_NAME_MAX_LENGTH)]
    public required string GameName { get; set; }
    public required int NumberOfPlayers { get; set; }
    public Guid GameGuid { get; set; }
    public required GameCreationStatus CreationStatus { get; set; }
    public required GamePlayStatus PlayStatus { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Should the map in this game wrap horizontally?
    /// </summary>
    /// <remarks>When a game is created, the player who creates the game can override the map creator's recommendation.</remarks>
    public bool HorizontalWrap { get; set; }

    /// <summary>
    /// Should the map in this game wrap vertically?
    /// </summary>
    /// <remarks>When a game is created, the player who creates the game can override the map creator's recommendation.</remarks>
    public bool VerticalWrap { get; set; }

    public ICollection<Player> Players { get; set; } = [];
}

