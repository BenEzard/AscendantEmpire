using AscendantEmpire.Domain.Entities.Games;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// An assembled unit design: exactly one body component plus the components fitted into its slots.
/// </summary>
/// <remarks>
/// <para>When <see cref="GameId"/> and <see cref="PlayerId"/> are both null the template is a global/common
/// template shared across all games. When set, it is a bespoke player-owned (custom) template within a game.</para>
/// </remarks>
public class UnitTemplate
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;
    public const int MAP_IMAGE_PATH_MAX_LENGTH = 500;

    public int Id { get; set; }

    /// <summary>
    /// Unique, stable key for global/common templates. Null for custom player-owned templates.
    /// </summary>
    [MaxLength(CODE_MAX_LENGTH)]
    public string? Code { get; set; }

    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Null for globally shared/common templates; set for bespoke player-owned templates within a game.
    /// </summary>
    public int? GameId { get; set; }

    /// <summary>
    /// Null for globally shared/common templates; set for bespoke player-owned templates.
    /// </summary>
    public int? PlayerId { get; set; }

    /// <summary>
    /// The single body component this template is built around.
    /// </summary>
    public int BodyComponentId { get; set; }

    /// <summary>
    /// Image to show on the Map.
    /// </summary>
    [MaxLength(MAP_IMAGE_PATH_MAX_LENGTH)]
    public required string MapImagePath { get; set; }

    public GameIndex? Game { get; set; }
    public Player? Player { get; set; }
    public UnitComponent BodyComponent { get; set; } = null!;

    /// <summary>
    /// The components fitted into the body's slots (one per filled slot).
    /// </summary>
    public ICollection<UnitTemplateComponent> Components { get; set; } = [];

    public ICollection<PlayerUnlockedTemplate> PlayerStates { get; set; } = [];
}
