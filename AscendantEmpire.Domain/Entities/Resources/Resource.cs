using AscendantEmpire.Domain.Enums.Resources;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Resources;

/// <summary>
/// The actual resource, e.g. Oak, Marble, Coal, Small Game Meat.
/// </summary>
public sealed class Resource
{
    /// <summary>
    /// Codes are hierarchical, so the limit has to hold a full dotted path rather than one segment - "ConstructionMaterials.Stone.Bricks.SandstoneBlock" is already 49.
    /// </summary>
    public const int MAX_CODE_LENGTH = 100;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;
    public const int MAX_ICON_PATH_LENGTH = 500;
    public const int MAX_DESCRIPTION_LENGTH = 1000;

    public int Id { get; set; }

    /// <summary>
    /// Unique stable key, hierarchical from the meta-category down, e.g. "ConstructionMaterials.Wood.Logs.OakLog".
    /// </summary>
    /// <remarks>
    /// What every upsert matches on, so it is what makes re-seeding the catalogue safe: a row whose DisplayName changes but whose code does not is renamed
    /// rather than duplicated. A graded Resource suffixes the grade, since all its grades share one DisplayName.
    /// </remarks>
    [MaxLength(MAX_CODE_LENGTH), Required]
    public required string Code { get; set; } = null!;

    [MaxLength(MAX_DISPLAY_NAME_LENGTH), Required]
    public required string DisplayName { get; set; } = null!;

    /// <summary>
    /// Flavour text describing what this Resource is and what it is used for.
    /// </summary>
    /// <remarks>Shown on the Resource Manager's General page. Null where none has been written yet, which the UI substitutes a placeholder for.</remarks>
    [MaxLength(MAX_DESCRIPTION_LENGTH)]
    public string? Description { get; set; }


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
    /// Display order of this Resource within its Group or Category.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The global default only. A Player can rearrange their own tickers, and that per-Player ordering takes precedence over this once it exists; until then
    /// this is the order everyone sees, and it is what stops a Player with no ordering of their own getting an arbitrary one.
    /// </para>
    /// <para>Spaced by 10 by the seed, so a Resource can be slotted between two others without renumbering the rest.</para>
    /// </remarks>
    public required int SortOrder { get; set; }

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
