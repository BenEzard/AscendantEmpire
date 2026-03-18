using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Optional middle grouping such as "Stone", "Wood", etc.
/// </summary>
public sealed class ResourceCategory
{
    public int Id { get; set; }
    public int ResourceMetaCategoryId { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [MaxLength(50)]
    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public ResourceMetaCategory MetaCategory { get; set; } = null!;
    public ICollection<ResourceGroup> ResourceGroups { get; set; } = [];
    public ICollection<FactionResourcePriority> FactionPriorities { get; set; } = [];
}