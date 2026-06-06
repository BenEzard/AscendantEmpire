using AscendantEmpire.Domain.Entities.Assets._not_used_yet;
using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Enums.Resources;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// The actual resource, e.g. Oak, Marble, Coal, Small Game Meat.
/// </summary>
public sealed class Resource
{
    public const int MAX_CODE_LENGTH = 50;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;
    public const int MAX_ICON_PATH_LENGTH = 500;

    public int Id { get; set; }

    /// <summary>
    /// Unique stable key, e.g. "oak", "marble"
    /// </summary>
    [MaxLength(MAX_CODE_LENGTH), Required]
    public required string Code { get; set; } = null!;

    [MaxLength(MAX_DISPLAY_NAME_LENGTH), Required]
    public required string DisplayName { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the file path of the icon.
    /// </summary>
    [MaxLength(MAX_ICON_PATH_LENGTH), Required]
    public required string IconPath { get; set; } = null!;

    /// <summary>
    /// Is this Resource graded? (Low, Medium, High)
    /// </summary>
    /// <remarks>
    /// <para>Note that this means if IsGradedResource is false, ResourceGrade should be None.</para>
    /// <para>If IsGradedResource is true, there will be Resources with each grade (Low, Medium, High).</para>
    /// </remarks>
    public required bool IsGradedResource { get; set; }

    /// <summary>
    /// The grade of this Resource if it is a graded resource. If IsGradedResource is false, this should be None.
    /// </summary>
    public ResourceGrade ResourceGrade { get; set; }

    /// <summary>
    /// Resource always belongs to a meta category.
    /// </summary>
    public int ResourceMetaCategoryId { get; set; }

    /// <summary>
    /// Set when this resource belongs to a <see cref="ResourceGroup"/> (grouped path).
    /// Mutually exclusive with <see cref="ResourceCategoryId"/>.
    /// </summary>
    public int? ResourceGroupId { get; set; }

    public ResourceGroup? Group { get; set; }

    /// <summary>
    /// Set when this resource belongs directly to a <see cref="ResourceCategory"/>
    /// whose <c>CategoryType</c> is <c>StandaloneResources</c> (standalone path).
    /// Mutually exclusive with <see cref="ResourceGroupId"/>.
    /// </summary>
    public int? ResourceCategoryId { get; set; }

    public ResourceCategory? Category { get; set; }

    /// <summary>
    /// If true, this Resource will never be treated as a luxury regardless of supply.
    /// </summary>
    public bool NeverLuxury { get; set; }

    public ResourceMetaCategory MetaCategory { get; set; } = null!;
}
