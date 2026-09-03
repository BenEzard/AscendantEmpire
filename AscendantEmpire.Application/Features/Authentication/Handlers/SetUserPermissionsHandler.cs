using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Authentication.Handlers;

/// <summary>
/// Sets the complete set of permissions granted to a user.
/// </summary>
/// <remarks>
/// <see cref="SetUserPermissionsRequest.Permissions"/> is the desired end state, so anything not listed is revoked. Passing an empty collection is
/// meaningful and strips every grant.
/// </remarks>
public record SetUserPermissionsRequest(
    int UserId,
    IReadOnlyCollection<PermissionType> Permissions
) : IRequest<ServiceResultBase>;

internal class SetUserPermissionsRequestValidator : Validator<SetUserPermissionsRequest>
{
    public SetUserPermissionsRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("User Id is required.");

        RuleFor(x => x.Permissions)
            .NotNull()
            .WithMessage("Permissions are required (send an empty list to revoke all).");

        RuleForEach(x => x.Permissions)
            .IsInEnum()
            .WithMessage("Each permission must be a valid permission value.");
    }
}

internal class SetUserPermissionsHandler(
    ILogger<SetUserPermissionsHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<SetUserPermissionsRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(SetUserPermissionsRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            bool userExists = await context.Users.AnyAsync(user => user.Id == request.UserId, cancelToken);
            if (userExists == false)
            {
                return ServiceResultBase.NotFound($"User {request.UserId} was not found.");
            }

            // The request is the complete desired state, so upsert by the natural key (user, permission):
            // leave rows that should stay, insert the ones that are new, delete the ones no longer wanted.
            // Untouched grants keep their id and their original GrantedAt, so re-saving an unchanged set is a no-op.
            HashSet<PermissionType> wanted = [.. request.Permissions];

            List<UserPermission> existing = await context.UserPermissions
                .Where(permission => permission.UserId == request.UserId)
                .ToListAsync(cancelToken);

            HashSet<PermissionType> alreadyHeld = [.. existing.Select(permission => permission.Permission)];

            DateTimeOffset grantedAt = DateTimeOffset.UtcNow;

            foreach (PermissionType permission in wanted.Where(permission => alreadyHeld.Contains(permission) == false))
            {
                context.UserPermissions.Add(new UserPermission
                {
                    UserId = request.UserId,
                    Permission = permission,
                    GrantedAt = grantedAt,
                });
            }

            List<UserPermission> revoked = [.. existing.Where(permission => wanted.Contains(permission.Permission) == false)];
            context.UserPermissions.RemoveRange(revoked);

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("Permissions for user {UserId} set to [{Permissions}].",
                request.UserId,
                string.Join(", ", wanted));

            return ServiceResultBase.SuccessNoContent();
        }
        catch (Exception ex)
        {
            const string error = "Unhandled exception setting user permissions.";

            logger.LogError(ex, error);

            return ServiceResultBase.Fail([error, ex.Message]);
        }
    }
}
