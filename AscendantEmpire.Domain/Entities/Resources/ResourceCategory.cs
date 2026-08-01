using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Domain.Entities.Resources;

/// <summary>
/// Optional middle grouping such as "Stone", "Wood", etc.
/// </summary>
public sealed class ResourceCategory
{
    public const int MAX_CODE_LENGTH = 50;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;

    public int Id { get; set; }

    public int ResourceMetaCategoryId { get; set; }

    /// <summary>
    /// Unique code for the Resource Category, e.g. "Stone" (within the <c>ResourceMetaCategory</c>).
    /// </summary>
    public required string Code { get; set; } = null!;

    /// <summary>
    /// Denotes the type of Categories this will hold (Standalone or Grouped)
    /// </summary>
    public required ResourceCategoryType CategoryType { get; set; }

    /// <summary>
    /// Display name, e.g. "Stone".
    /// </summary>
    public required string DisplayName { get; set; } = null!;

    /// <summary>
    /// Sort order for display.
    /// </summary>
    public required int SortOrder { get; set; }

    /// <summary>
    /// A ResourceCategory must belong to a ResourceMetaCategory
    /// </summary>
    public ResourceMetaCategory MetaCategory { get; set; } = null!;

    /// <summary>
    /// ResourceGroups within this category (GroupedResources categories only).
    /// </summary>
    public ICollection<ResourceGroup> ResourceGroups { get; set; } = [];

    /// <summary>
    /// Resources that belong directly to this category (StandaloneResources categories only).
    /// </summary>
    public ICollection<Resource> Resources { get; set; } = [];
}