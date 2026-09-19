using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// A group of like MapResourceTypes, e.g. Food, Metals or Wood, so the map editor can list them together.
/// </summary>
/// <remarks>
/// Purely for arranging the catalogue: nothing in play reads it. It is deliberately not the <c>resources.ResourceMetaCategory</c> of what a type yields, since one type can
/// yield Resources from several meta categories (a Deer gives both meat and a pelt).
/// </remarks>
public class MapResourceCategory
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    /// <summary>
    /// Unique, stable key, e.g. "Food". What seed data matches on.
    /// </summary>
    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    /// <summary>
    /// The name shown in the map editor, e.g. "Food".
    /// </summary>
    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Where the category comes in the map editor's list, lowest first.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// The types in this category.
    /// </summary>
    public ICollection<MapResourceType> MapResourceTypes { get; set; } = [];
}
