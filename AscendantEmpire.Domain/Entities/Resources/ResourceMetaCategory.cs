namespace AscendantEmpire.Domain.Entities.Resources;

/// <summary>
/// Top-level grouping of Resources e.g. "Construction Materials", "Food", "Industrial Inputs", etc.
/// </summary>
public sealed class ResourceMetaCategory
{
    public const int MAX_CODE_LENGTH = 50;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;
    public const int MAX_ICON_PATH_LENGTH = 500;

    public int Id { get; set; }

    /// <summary>
    /// Unique code for the resource meta-category.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Display name, e.g. "Construction Materials".
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional sort order for display. 
    /// </summary>
    /// <remarks>The smaller the number, the closer to the left it will be displayed.</remarks>
    public required int SortOrder { get; set; }

    /// <summary>
    /// Path to an icon file.
    /// </summary>
    public required string IconPath { get; set; }

    public ICollection<ResourceCategory> Categories { get; set; } = [];
}
