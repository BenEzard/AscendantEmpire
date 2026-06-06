using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.Resources;

public sealed class ResourceGroup
{
    public const int MAX_CODE_LENGTH = 50;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;

    public int Id { get; set; }

    /// <summary>
    /// Unique code for the Resource Group, e.g. "Logs" (within the <c>ResourceCategory</c>).
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Display name, e.g. "Logs". (e.g. within "Construction Materials" (ResourceMetaCategory) -> "Wood" (ResourceCategory) -> "Logs")
    /// </summary>
    public required string DisplayName { get; set; }

    public int ResourceCategoryId { get; set; }

    /// <summary>
    /// Display order of this group within its Category. If not specified, will be based on Name.
    /// </summary>
    public int? SortOrder { get; set; }

    public required ResourceCategory Category { get; set; }

    public ICollection<Resource> Resources { get; set; } = [];
}
