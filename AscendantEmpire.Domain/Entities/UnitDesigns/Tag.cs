using AscendantEmpire.Domain.Enums.UnitDesigns;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A global, reusable tag attached to components — either a structural classification (its family/role)
/// or a bonus, distinguished by <see cref="TagType"/>.
/// </summary>
public class Tag
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;
    public const int DESCRIPTION_MAX_LENGTH = 200;

    public int Id { get; set; }

    /// <summary>
    /// Unique, stable key for the tag, e.g. "amphibious", "stealth".
    /// </summary>
    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    [MaxLength(DESCRIPTION_MAX_LENGTH)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this tag is a structural classification or a bonus (buff/perk).
    /// </summary>
    public required TagType TagType { get; set; }

    public ICollection<UnitComponentTag> ComponentTags { get; set; } = [];
}
