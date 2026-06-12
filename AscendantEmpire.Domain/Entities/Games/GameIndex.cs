using AscendantEmpire.Domain.Enums.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Games;

public class GameIndex
{
    public const int GAME_NAME_MAX_LENGTH = 250;
    public const int MIN_BATTLE_MAP_SIZE  = 30;
    public const int MAX_BATTLE_MAP_SIZE  = 60;

    [Key]
    public int Id { get; set; }
    public int CampaignMapIndexId { get; set; }
    [MaxLength(GAME_NAME_MAX_LENGTH)]
    public required string GameName { get; set; }
    public required int NumberOfPlayers { get; set; }
    public required GameCreationStatus CreationStatus { get; set; }
    public required GamePlayStatus PlayStatus { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Seed used to generate biome diversity in all BattleMaps for this game.
    /// </summary>
    /// <remarks>
    /// Terrain altitude is derived from the shared campaign Perlin field (ensuring
    /// edge continuity), but biome distribution is seeded per-game so two games on
    /// the same campaign map produce visually distinct battle maps.
    /// Always populated — auto-generated randomly if not supplied at game creation.
    /// </remarks>
    public required long GenerationSeed { get; set; }

    /// <summary>
    /// Width and height (in tiles) of every BattleMap generated in this game.
    /// BattleMaps are always square; this value applies to both dimensions.
    /// </summary>
    public required int BattleMapSize { get; set; }

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

