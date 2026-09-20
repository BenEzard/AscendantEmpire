using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Classes;

/// <summary>
/// One selectable part of the database seed, as the seed selection dialog needs it.
/// </summary>
/// <remarks>
/// Projected straight off the registered seed steps, so the dialog's rows, their grouping and the dependency edges it ticks for the user all come from the
/// same place the seed itself runs from. Nothing here is hard-coded in the frontend.
/// </remarks>
public class DTSeedTarget
{
    /// <summary>
    /// The target's stable key, sent back on <c>POST /system/seed</c>. Serialised as its enum name.
    /// </summary>
    public required SeedTarget Target { get; init; }

    /// <summary>
    /// The heading this target is listed under.
    /// </summary>
    public required SeedTargetGroup Group { get; init; }

    /// <summary>
    /// The label shown against the checkbox.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// One line describing what this target writes, shown under its label.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The targets that must be seeded first. Direct prerequisites only — the dialog walks them transitively, as the server does.
    /// </summary>
    public required IReadOnlyCollection<SeedTarget> DependsOn { get; init; }

    /// <summary>
    /// Position within the group, ascending.
    /// </summary>
    public required int SortOrder { get; init; }

    /// <summary>
    /// How many rows this target would write, for the count shown beside its label.
    /// </summary>
    public required int ItemCount { get; init; }
}
