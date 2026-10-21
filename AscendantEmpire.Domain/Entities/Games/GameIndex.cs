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
    /// <remarks>
    /// Which sizes are allowed is configuration rather than anything this entity states: the bounds, and the increment every size must be a multiple
    /// of, live in <c>GameSettings:BattleMapSettings</c> and are enforced by <c>SetupGameHandler</c> when the game is created.
    /// </remarks>
    public required int BattleMapSize { get; set; }

    /// <summary>
    /// The <see cref="CampaignMap.CampaignMapId"/> of this game's tile at coordinate (0, 0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each game's campaign map occupies one contiguous block of ids, so any tile's id is
    /// <c>CampaignMapOffsetId + XCoord + YCoord * MapWidth</c>. The application assigns those ids
    /// during setup rather than leaving them to an IDENTITY column, so the relationship holds by
    /// construction instead of depending on the order the rows happened to be inserted in.
    /// </para>
    /// <para>
    /// This is the world-map counterpart of <see cref="Player.CampaignOffsetId"/>, which does the
    /// same job for one player's <see cref="PlayerCampaignMap"/> block.
    /// </para>
    /// </remarks>
    public int CampaignMapOffsetId { get; set; }

    /// <summary>
    /// Width (in tiles) of this game's campaign map.
    /// </summary>
    /// <remarks>
    /// Snapshotted from the gallery map during setup rather than read back through
    /// <see cref="CampaignMapIndexId"/>, because a single gallery row is shared by many games:
    /// resizing it would otherwise silently change how every existing game's tile ids decode
    /// back into coordinates.
    /// </remarks>
    public int MapWidth { get; set; }

    /// <summary>
    /// Height (in tiles) of this game's campaign map.
    /// </summary>
    /// <remarks>Snapshotted at setup for the same reason as <see cref="MapWidth"/>.</remarks>
    public int MapHeight { get; set; }

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

