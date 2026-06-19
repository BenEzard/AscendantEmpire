using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A global, reusable bonus/buff tag that can be attached to components.
/// </summary>
public class Tag
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

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
    public string? Description { get; set; }

    public ICollection<UnitComponentTag> ComponentTags { get; set; } = [];
}
