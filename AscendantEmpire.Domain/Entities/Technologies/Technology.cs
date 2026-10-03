using AscendantEmpire.Domain.Enums.Technologies;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One researchable technology in the global catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Authored catalogue content, shared by every game and every Player. A Player's own progress and their own requirement figures live on
/// <see cref="PlayerTechnology"/>; see <c>docs/design/technology.md</c> for the three-bucket model.
/// </para>
/// <para>
/// <b>The three requirement columns here are authoring input only.</b> They are the base figures the requirement generator reads once, at game setup, to
/// produce each Player's own numbers. No gameplay query may read them - completion is always tested against the Player's row, never by joining back here, which
/// is what keeps the randomised customisation modes a change to one generator rather than a rewrite of every query.
/// </para>
/// </remarks>
public sealed class Technology
{
    /// <summary>
    /// Codes are hierarchical, so the limit has to hold a full dotted path rather than one segment.
    /// </summary>
    public const int MAX_CODE_LENGTH = 100;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;
    public const int MAX_DESCRIPTION_LENGTH = 1000;
    public const int MAX_ICON_PATH_LENGTH = 500;

    public int Id { get; set; }

    /// <summary>
    /// Unique stable key, hierarchical from the era down, e.g. "Bronze.Metalworking.Smelting".
    /// </summary>
    /// <remarks>
    /// What every upsert matches on, so it is what makes re-seeding the catalogue safe: a row whose DisplayName changes but whose code does not is renamed
    /// rather than duplicated. Also what the starter-technology list in game setup resolves against.
    /// </remarks>
    [MaxLength(MAX_CODE_LENGTH), Required]
    public required string Code { get; set; } = null!;

    [MaxLength(MAX_DISPLAY_NAME_LENGTH), Required]
    public required string DisplayName { get; set; } = null!;

    /// <summary>
    /// Flavour text describing what this Technology is and what it leads to.
    /// </summary>
    /// <remarks>Null where none has been written yet, which the UI substitutes a placeholder for.</remarks>
    [MaxLength(MAX_DESCRIPTION_LENGTH)]
    public string? Description { get; set; }

    [MaxLength(MAX_ICON_PATH_LENGTH), Required]
    public required string IconPath { get; set; } = null!;

    /// <summary>
    /// The age of the tree this Technology belongs to.
    /// </summary>
    /// <remarks>
    /// Read by the <c>RandomisedEqual</c> requirement generator and by nothing else - see <see cref="TechnologyEra"/>, which explains why. It is not a UI
    /// grouping, and the tree has no category or branch dimension at all.
    /// </remarks>
    public required TechnologyEra Era { get; set; }

    /// <summary>
    /// Display order within the era.
    /// </summary>
    /// <remarks>Spaced by 10 by the seed, so a Technology can be slotted between two others without renumbering the rest.</remarks>
    public required int SortOrder { get; set; }

    /// <summary>
    /// Authored horizontal position in the tech-tree diagram, or <see langword="null"/> to let the client lay it out.
    /// </summary>
    /// <remarks>
    /// Prerequisite edges alone do not produce a stable layout - adding one technology reshuffles an auto-laid-out tree. These two columns exist so the
    /// arrangement can be pinned once it is worth pinning; until then they stay null and the client arranges by prerequisite depth.
    /// </remarks>
    public int? TreeColumn { get; set; }

    /// <summary>
    /// Authored vertical position in the tech-tree diagram, or <see langword="null"/> to let the client lay it out.
    /// </summary>
    public int? TreeRow { get; set; }

    /// <summary>
    /// Base directed-research points required, or <see langword="null"/> when this Technology does not use the directed bucket.
    /// </summary>
    /// <remarks>
    /// Null means the bucket is unused, not that it is free. Which buckets a Technology uses is a property of the Technology and is never randomised, so the
    /// nullability carries across to every Player's row unchanged.
    /// </remarks>
    public int? DirectedRequired { get; set; }

    /// <summary>
    /// Base experience points required, or <see langword="null"/> when this Technology does not use the experience bucket.
    /// </summary>
    public int? ExperienceRequired { get; set; }

    /// <summary>
    /// Base diffusion points required, or <see langword="null"/> when this Technology does not use the diffusion bucket.
    /// </summary>
    public int? DiffusionRequired { get; set; }

    /// <summary>
    /// The Assets and quantities that must be paid before directed research can begin. Empty means free to unlock.
    /// </summary>
    public ICollection<TechnologyAcquisitionCost> AcquisitionCosts { get; set; } = [];

    /// <summary>
    /// Edges to the technologies this one requires.
    /// </summary>
    public ICollection<TechnologyPrerequisite> Prerequisites { get; set; } = [];

    /// <summary>
    /// Edges from the technologies that require this one. What the discovery cascade walks.
    /// </summary>
    public ICollection<TechnologyPrerequisite> Dependents { get; set; } = [];

    /// <summary>
    /// What discovering this Technology unlocks or retires.
    /// </summary>
    public ICollection<TechnologyGrant> Grants { get; set; } = [];

    /// <summary>
    /// Which kinds of experience feed this Technology's experience bucket.
    /// </summary>
    public ICollection<TechnologyExperienceType> ExperienceTypes { get; set; } = [];
}
