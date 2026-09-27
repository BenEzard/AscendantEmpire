using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.BackendContracts.Resources;

public record UpsertResourceDto
{
    /// <summary>
    /// Unique code for the Resource, e.g. "ConstructionMaterials.Wood.Logs.OakLog".
    /// </summary>
    /// <remarks>
    /// <para>If not supplied, code is derived automatically by normalising DisplayName with non-alpha characters removed and ProperCase applied.</para>
    /// <para>
    /// A graded Resource must supply one, because every grade of it shares a DisplayName and so would derive the same code. Suffix the grade to tell them
    /// apart - "Weapons.MeleeWeapons.BoneKnife.Low".
    /// </para>
    /// </remarks>
    public string? Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Oak Log".
    /// </summary>
    /// <remarks>Shared by every grade of a graded Resource - the grade is not part of the name.</remarks>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Flavour text describing what this Resource is and what it is used for.
    /// </summary>
    /// <remarks>Only overwritten when supplied, so an update that omits it leaves any existing description alone.</remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Path to an icon file, as a URL the frontend can resolve.
    /// </summary>
    /// <remarks>
    /// Served from the frontend's <c>public</c> folder rather than bundled, so it is a root-relative URL with forward slashes -
    /// "/resources/wood/oak-log_40px.png". Omit it for a Resource with no icon.
    /// </remarks>
    public string? IconPath { get; init; }

    /// <summary>
    /// Optional sort order for display within the Resource's Group or Category.
    /// </summary>
    /// <remarks>If not provided, it will be calculated automatically based on the order of the resources in the input list, with gaps for future insertions.</remarks>
    public int? SortOrder { get; init; }

    /// <summary>
    /// Whether this Resource comes in grades, in which case one row is expected per grade.
    /// </summary>
    public bool IsGradedResource { get; init; } = false;

    /// <summary>
    /// The grade of this Resource, or <c>None</c> when <see cref="IsGradedResource"/> is false.
    /// </summary>
    public ResourceGrade ResourceGrade { get; init; } = ResourceGrade.None;

    /// <summary>
    /// Whether this Resource can never be treated as a luxury, however scarce it becomes.
    /// </summary>
    public bool NeverLuxury { get; init; } = false;
}
