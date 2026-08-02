using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Authentication.Services;

/// <summary>
/// Shared read of a user's granted permissions.
/// </summary>
/// <remarks>
/// Every path that mints an access token has to stamp the same permission set into it — register, login, enter-game and refresh — so the query lives in one
/// place rather than being copied four times. It is an extension on the context rather than an injected service because all four callers already have one
/// open, and opening a second connection to read five integers would be wasteful.
/// </remarks>
public static class UserPermissionQueries
{
    /// <summary>
    /// Reads the permissions currently granted to a user. Returns an empty collection for a user with none.
    /// </summary>
    public static async Task<IReadOnlyCollection<PermissionType>> GetUserPermissionsAsync(this AscendantEmpireContext context,
        int userId,
        CancellationToken cancelToken)
    {
        return await context.UserPermissions
            .AsNoTracking()
            .Where(permission => permission.UserId == userId)
            .Select(permission => permission.Permission)
            .OrderBy(permission => permission)
            .ToListAsync(cancelToken);
    }
}
