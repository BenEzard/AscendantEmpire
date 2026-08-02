using AscendantEmpire.Domain.Enums.Authentication;

namespace AscendantEmpire.BackendContracts.Authentication.GetUsers;

public record class AuthUser
{
    public int Id { get; init; }

    public required string Username { get; init; }

    public string? Email { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? InactiveAt { get; init; }

    public InactiveReasonType InactiveReasonType { get; init; }

    /// <summary>
    /// The capabilities currently granted to this account.
    /// </summary>
    /// <remarks>
    /// Included in the list rather than fetched per user, so the System panel's permissions dialog opens on data it already has.
    /// </remarks>
    public required IReadOnlyCollection<PermissionType> Permissions { get; init; }
}