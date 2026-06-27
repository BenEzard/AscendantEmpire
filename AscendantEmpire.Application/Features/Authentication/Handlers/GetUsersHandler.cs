using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Authentication.GetUsers;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Authentication.Handlers;

/// <summary>
/// Returns the current auth user records without exposing password hashes or session data.
/// </summary>
public record GetUsersRequest() : IRequest<ServiceResult<IReadOnlyCollection<AuthUser>>>;

internal class GetUsersHandler(
    ILogger<GetUsersHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetUsersRequest, ServiceResult<IReadOnlyCollection<AuthUser>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<AuthUser>>> Handle(GetUsersRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<AuthUser> users = await context.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)
                .Select(u => new AuthUser
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    CreatedAt = u.CreatedAt,
                    InactiveAt = u.InactiveAt,
                    InactiveReasonType = u.InactiveReasonType,
                })
                .ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<AuthUser>>.Succeed(users);
        }
        catch (Exception ex)
        {
            const string error = "Unhandled exception getting auth users.";

            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<AuthUser>>.Fail([error, ex.Message]);
        }
    }
}