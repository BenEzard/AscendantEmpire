using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// One Technology to create or update in the catalogue, with its costs, prerequisites, grants and experience mappings.
/// </summary>
/// <remarks>
/// <para>
/// Matched on <see cref="Code"/>, so a Technology whose DisplayName changes but whose code does not is renamed rather than duplicated.
/// </para>
/// <para>
/// The four child collections are <b>replaced wholesale</b>, not merged: whatever is supplied becomes the Technology's complete set, and an empty collection
/// clears it. Merging would make it impossible to remove a prerequisite or a grant through the same path that added it.
/// </para>
/// </remarks>
public record UpsertTechnologyDto
{
    /// <summary>
    /// Unique code for the Technology, hierarchical from the era down, e.g. "Bronze.Metalworking.Smelting".
    /// </summary>
    /// <remarks>
    /// Required rather than derived, unlike an Asset's: prerequisites reference it, so it is a cross-reference key and must not shift when a display name is
    /// reworded.
    /// </remarks>
    public required string Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Smelting".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Flavour text describing what this Technology is and what it leads to.
    /// </summary>
    /// <remarks>Only overwritten when supplied, so an update that omits it leaves any existing description alone.</remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Path to an icon file, as a root-relative URL the frontend can resolve.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// The age of the tree this Technology belongs to.
    /// </summary>
    public required TechnologyEra Era { get; init; }

    /// <summary>
    /// Display order within the era.
    /// </summary>
    /// <remarks>
    /// Derived from the position among the supplied technologies <i>of the same era</i> when omitted, because the uniqueness constraint is on (Era, SortOrder)
    /// rather than on SortOrder alone.
    /// </remarks>
    public int? SortOrder { get; init; }

    /// <summary>
    /// Authored horizontal position in the tech-tree diagram, or null to let the client lay it out.
    /// </summary>
    public int? TreeColumn { get; init; }

    /// <summary>
    /// Authored vertical position in the tech-tree diagram, or null to let the client lay it out.
    /// </summary>
    public int? TreeRow { get; init; }

    /// <summary>
    /// Base directed-research points required, or null when this Technology does not use the directed bucket.
    /// </summary>
    /// <remarks>
    /// Null means the bucket is unused, not free. At least one of the three buckets must be non-null, or the Technology could never be discovered by any
    /// means.
    /// </remarks>
    public int? DirectedRequired { get; init; }

    /// <summary>
    /// Base experience points required, or null when this Technology does not use the experience bucket.
    /// </summary>
    public int? ExperienceRequired { get; init; }

    /// <summary>
    /// Base diffusion points required, or null when this Technology does not use the diffusion bucket.
    /// </summary>
    public int? DiffusionRequired { get; init; }

    /// <summary>
    /// The Assets and quantities that must be paid to unlock it. Empty means free.
    /// </summary>
    public IReadOnlyCollection<UpsertTechnologyAcquisitionCostDto> AcquisitionCosts { get; init; } = [];

    /// <summary>
    /// The technologies that must be discovered first.
    /// </summary>
    public IReadOnlyCollection<UpsertTechnologyPrerequisiteDto> Prerequisites { get; init; } = [];

    /// <summary>
    /// What discovering it unlocks or retires.
    /// </summary>
    public IReadOnlyCollection<UpsertTechnologyGrantDto> Grants { get; init; } = [];

    /// <summary>
    /// The codes of the experience types that feed its experience bucket.
    /// </summary>
    /// <remarks>
    /// Should be empty exactly when <see cref="ExperienceRequired"/> is null: a Technology with an experience requirement and no sources could never fill it,
    /// and sources on a Technology with no experience bucket have nowhere to go.
    /// </remarks>
    public IReadOnlyCollection<string> ExperienceTypeCodes { get; init; } = [];
}
