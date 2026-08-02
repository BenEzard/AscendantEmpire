using AscendantEmpire.Domain.Enums.Authentication;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Identity;

/// <summary>
/// One permission granted to one <see cref="User"/>. A user holds a permission when a row exists; revoking deletes the row.
/// </summary>
/// <remarks>
/// Presence-as-truth keeps the table append-and-delete only, so there is no "granted but disabled" state to reason about. The pair
/// (<see cref="UserId"/>, <see cref="Permission"/>) is unique, enforced by an index in the DbContext.
/// </remarks>
public class UserPermission
{
    [Key]
    public int Id { get; set; }

    /// <summary>The account the permission is granted to.</summary>
    public required int UserId { get; set; }

    /// <summary>Navigation to the owning user.</summary>
    public User User { get; set; } = null!;

    /// <summary>The granted capability.</summary>
    public required PermissionType Permission { get; set; }

    /// <summary>When the permission was granted. Kept for auditing who gained access when; never used in an authorisation decision.</summary>
    public required DateTimeOffset GrantedAt { get; set; }
}
