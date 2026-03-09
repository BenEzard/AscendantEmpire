using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Top-level grouping of Resources e.g. "Construction Materials", "Food", "Industrial Inputs", etc.
/// </summary>
public sealed class ResourceMetaCategory
{
    public int Id { get; set; }
    
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [MaxLength(50)]
    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets the file path of the icon.
    /// </summary>
    [MaxLength(500)]
    public string? IconPath { get; set; } = null!;

    public ICollection<ResourceCategory> Categories { get; set; } = [];
}
