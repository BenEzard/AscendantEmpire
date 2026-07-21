using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;

/// <summary>
/// One tag type's values in a bulk upsert (PUT) of the tag type catalog. <see cref="TagTypeId"/>
/// identifies which row to create or update.
/// </summary>
public record UpsertTagTypeDto
{
    public required TagType TagTypeId { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    /// <summary>
    /// Whether tags of this type are inherited. Stored/retrieved only — no behaviour attached yet.
    /// </summary>
    public required bool IsInherited { get; init; }

    /// <summary>
    /// Optional HTML colour string used to present the tag type, e.g. "#3366cc".
    /// </summary>
    public string? Colour { get; init; }

    /// <summary>
    /// Optional icon identifier used to present the tag type.
    /// </summary>
    public string? Icon { get; init; }
}
