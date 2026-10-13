using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Authentication.GetUsers;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Authentication.Handlers;

/// <summary>
/// Returns the current auth user records without exposing password hashes or session data.
/// </summary>
public record GetUsersRequest() : IRequest<ServiceResult<IReadOnlyCollection<AuthUser>>>;

internal class GetUsersHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetUsersRequest, ServiceResult<IReadOnlyCollection<AuthUser>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<AuthUser>>> Handle(GetUsersRequest request, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Read the grants as one flat query and group them in memory, rather than projecting a nested
        // collection per user. Two simple round trips beat one query whose translation depends on the
        // provider, and the volumes here are an administrator's user list either way.
        Dictionary<int, List<PermissionType>> permissionsByUser = (await context.UserPermissions
                .AsNoTracking()
                .OrderBy(p => p.Permission)
                .Select(p => new { p.UserId, p.Permission })
                .ToListAsync(cancelToken))
            .GroupBy(p => p.UserId)
            .ToDictionary(group => group.Key, group => group.Select(p => p.Permission).ToList());

        // Projected into an anonymous row first, then assembled with the permissions in memory — a
        // collection expression can't appear inside an expression tree, so AuthUser can't be built
        // directly in the query.
        var rows = await context.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.CreatedAt,
                u.InactiveAt,
                u.InactiveReasonType,
            })
            .ToListAsync(cancelToken);

        List<AuthUser> users = [.. rows.Select(row => new AuthUser
        {
            Id = row.Id,
            Username = row.Username,
            Email = row.Email,
            CreatedAt = row.CreatedAt,
            InactiveAt = row.InactiveAt,
            InactiveReasonType = row.InactiveReasonType,
            Permissions = permissionsByUser.TryGetValue(row.Id, out List<PermissionType>? held) ? held : [],
        })];

        return ServiceResult<IReadOnlyCollection<AuthUser>>.Succeed(users);
    }
}