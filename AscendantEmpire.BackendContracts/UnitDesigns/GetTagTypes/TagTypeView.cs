using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetTagTypes;

/// <summary>
/// A single tag type's editable metadata as seen by the dev Unit Designer's tag type manager.
/// </summary>
public record TagTypeView
{
    /// <summary>
    /// The tag type this row describes (also its key).
    /// </summary>
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
