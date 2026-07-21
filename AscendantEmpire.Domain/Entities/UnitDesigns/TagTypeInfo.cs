using AscendantEmpire.Domain.Enums.UnitDesigns;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Editable metadata for a single <see cref="TagType"/> value — its display name, description, and
/// presentation (colour/icon) — authored through the dev Entity Setup Tool's Tag Type manager.
/// </summary>
/// <remarks>
/// The table is keyed by the <see cref="Enums.UnitDesigns.TagType"/> enum itself (no surrogate key), so
/// the fixed set of rows mirrors the enum members. The entity is named <c>TagTypeInfo</c> rather than
/// <c>TagType</c> to avoid colliding with the enum it stores as its key (following the
/// <c>CampaignBiome</c>/<c>CampaignGeography</c> precedent); it still maps to a table named "TagType".
/// </remarks>
public class TagTypeInfo
{
    public const int DISPLAY_NAME_MAX_LENGTH = 100;
    public const int DESCRIPTION_MAX_LENGTH = 200;
    public const int COLOUR_MAX_LENGTH = 50;
    public const int ICON_MAX_LENGTH = 50;

    /// <summary>
    /// The tag type this row describes. Also the primary key — assigned from the enum, never generated.
    /// </summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public TagType TagTypeId { get; set; }

    /// <summary>
    /// User-facing name for the tag type, e.g. "Classification".
    /// </summary>
    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Free-text description of what the tag type represents.
    /// </summary>
    [MaxLength(DESCRIPTION_MAX_LENGTH)]
    public required string Description { get; set; }

    /// <summary>
    /// Whether tags of this type are inherited. Stored/retrieved only — no behaviour is attached yet.
    /// </summary>
    public bool IsInherited { get; set; }

    /// <summary>
    /// Optional HTML colour string used to present the tag type, e.g. "#3366cc".
    /// </summary>
    [MaxLength(COLOUR_MAX_LENGTH)]
    public string? Colour { get; set; }

    /// <summary>
    /// Optional icon identifier used to present the tag type (same convention as a map editor icon).
    /// </summary>
    [MaxLength(ICON_MAX_LENGTH)]
    public string? Icon { get; set; }
}
