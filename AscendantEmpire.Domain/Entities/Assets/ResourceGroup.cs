using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

public sealed class ResourceGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    // NEW: required FK to exactly one category
    public int ResourceCategoryId { get; set; }

    /// <summary>
    /// Order of this group within its category.
    /// </summary>
    public int GroupOrder { get; set; }

    [MaxLength(2000)]
    public string? DevNote { get; set; } = null!;

    public ResourceCategory Category { get; set; } = null!;

    public ICollection<Resource> Resources { get; set; } = [];
    public ICollection<FactionResourcePriority> FactionPriorities { get; set; } = [];
}
