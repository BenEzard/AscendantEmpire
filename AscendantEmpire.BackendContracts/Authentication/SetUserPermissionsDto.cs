using AscendantEmpire.Domain.Enums.Authentication;

namespace AscendantEmpire.BackendContracts.Authentication;

/// <summary>
/// Replaces the set of permissions granted to one user.
/// </summary>
/// <remarks>
/// <see cref="Permissions"/> is the complete desired state, not a delta: anything omitted is revoked. An empty collection is valid and strips every
/// permission the user has.
/// </remarks>
public record SetUserPermissionsDto
{
    public required int UserId { get; init; }

    public required IReadOnlyCollection<PermissionType> Permissions { get; init; }
}
