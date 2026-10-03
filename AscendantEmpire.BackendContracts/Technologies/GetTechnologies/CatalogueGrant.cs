using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One thing a technology unlocks or retires when it is discovered.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TargetType"/> decides which catalogue <see cref="TargetId"/> belongs to, and the code and display name are resolved server-side - the
/// alternative would be a client fetching all five catalogues to render one row.
/// </para>
/// <para>
/// <see cref="TargetCode"/> and <see cref="TargetDisplayName"/> are null when the target no longer exists. The grant carries no foreign key, because it points
/// at a different table per target type, so a target deleted after the grant was authored leaves the row dangling - and showing that plainly is better than
/// hiding it.
/// </para>
/// </remarks>
public record CatalogueGrant
{
    public required TechnologyGrantTargetType TargetType { get; init; }

    public required int TargetId { get; init; }

    /// <summary>
    /// The target's code, or null when the target no longer exists.
    /// </summary>
    public string? TargetCode { get; init; }

    /// <summary>
    /// The target's display name, or null when the target no longer exists.
    /// </summary>
    public string? TargetDisplayName { get; init; }

    public required TechnologyGrantEffect GrantEffect { get; init; }
}
