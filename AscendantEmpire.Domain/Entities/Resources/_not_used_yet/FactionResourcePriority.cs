using AscendantEmpire.Domain.Entities.Resources;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Global per-faction ordering of resources within a category.
/// Lower number = consumed first.
/// </summary>
public sealed class FactionResourcePriority
{
    public int FactionId { get; set; }
    public int ResourceCategoryId { get; set; }
    public int ResourceId { get; set; }

    public int PriorityOrder { get; set; }

    public ResourceCategory Category { get; set; } = null!;
    public Resource Resource { get; set; } = null!;
}
