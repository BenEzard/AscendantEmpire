using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Grants every permission to the configured administrator at startup, but only while no account holds
/// <see cref="PermissionType.SystemPanel"/>.
/// </summary>
/// <remarks>
/// <para>
/// Permissions are handed out from the System panel, and reaching the System panel needs a permission — so on a database where nobody has one, there is no
/// way in. This closes that loop without a manual SQL step, and self-heals if every administrator is ever accidentally demoted.
/// </para>
/// <para>
/// Deliberately silent and non-fatal. It runs before anything has served a request, so a database that is unreachable, un-migrated, or simply not the one
/// this process cares about must not stop the host from starting — the API is perfectly able to serve 401s and health traffic without it. The test host
/// boots with a connection string that points nowhere, and relies on exactly this.
/// </para>
/// </remarks>
public sealed class PermissionBootstrapService(
    ILogger<PermissionBootstrapService> logger,
    IOptions<BootstrapSettings> settings,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string adminUsername = settings.Value.AdminUsername.Trim();

        // Nothing configured is the normal steady state once a real administrator exists, so this isn't worth a warning.
        if (string.IsNullOrEmpty(adminUsername))
        {
            logger.LogDebug("Permission bootstrap skipped: no Bootstrap:AdminUsername configured.");
            return;
        }

        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            bool anyAdministrator = await context.UserPermissions
                .AnyAsync(permission => permission.Permission == PermissionType.SystemPanel, cancellationToken);

            if (anyAdministrator)
            {
                logger.LogDebug("Permission bootstrap skipped: an account already holds {Permission}.", PermissionType.SystemPanel);
                return;
            }

            User? administrator = await context.Users
                .FirstOrDefaultAsync(user => user.Username == adminUsername, cancellationToken);

            if (administrator is null)
            {
                logger.LogWarning("Permission bootstrap skipped: no user named {Username} exists to grant permissions to.", adminUsername);
                return;
            }

            // Nobody holds SystemPanel, but this user may already hold some of the others — only add what's missing
            // so the unique index can't be violated.
            HashSet<PermissionType> alreadyHeld = [.. await context.UserPermissions
                .Where(permission => permission.UserId == administrator.Id)
                .Select(permission => permission.Permission)
                .ToListAsync(cancellationToken)];

            DateTimeOffset grantedAt = DateTimeOffset.UtcNow;

            List<PermissionType> granted = [];

            foreach (PermissionType permission in Enum.GetValues<PermissionType>())
            {
                if (alreadyHeld.Contains(permission))
                {
                    continue;
                }

                context.UserPermissions.Add(new UserPermission
                {
                    UserId = administrator.Id,
                    Permission = permission,
                    GrantedAt = grantedAt,
                });

                granted.Add(permission);
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogWarning("Permission bootstrap granted [{Permissions}] to {Username} (Id {UserId}) because no account held {SystemPanel}.",
                string.Join(", ", granted),
                administrator.Username,
                administrator.Id,
                PermissionType.SystemPanel);
        }
        catch (Exception ex)
        {
            // Never fatal — see the class remarks. Logged loudly so a genuinely broken bootstrap is visible.
            logger.LogError(ex, "Permission bootstrap failed. No permissions were granted; the application will start regardless.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
