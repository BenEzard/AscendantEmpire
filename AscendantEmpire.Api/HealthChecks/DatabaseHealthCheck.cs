using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AscendantEmpire.Api.HealthChecks;

/// <summary>
/// Reports whether the application's database can be reached, which is the one dependency the readiness probe consults.
/// </summary>
/// <remarks>
/// <para>
/// Written against <see cref="IDbContextFactory{TContext}"/> rather than taken from
/// <c>Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore</c>, whose <c>AddDbContextCheck</c> resolves the context itself out of the
/// container. This API registers only the factory — <c>AddDbContextFactory</c> does not also register <see cref="AscendantEmpireContext"/> as a scoped
/// service — so the packaged check would have nothing to resolve when a probe arrived, and would report the database unreachable for a reason that has
/// nothing to do with the database.
/// </para>
/// <para>
/// <c>CanConnectAsync</c> opens a connection and discards it rather than running a query of our own. That is deliberately the cheapest thing that still
/// proves something: a probe is hit on a schedule and must not cost a table scan, and "the server answered and the database is there" is exactly what
/// separates a process worth sending traffic to from one that will fail every request it is given.
/// </para>
/// </remarks>
public sealed class DatabaseHealthCheck(IDbContextFactory<AscendantEmpireContext> dbFactory) : IHealthCheck
{
    /// <summary>
    /// The name the check is registered under, and the key it appears under in a verbose probe response.
    /// </summary>
    public const string CHECK_NAME = "database";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AscendantEmpireContext dbContext = await dbFactory.CreateDbContextAsync(cancellationToken);

            bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (canConnect)
            {
                return HealthCheckResult.Healthy("The database accepted a connection.");
            }

            return HealthCheckResult.Unhealthy("The database refused a connection.");
        }
        catch (Exception exception)
        {
            // HealthCheckService catches an escaping exception itself and reports Unhealthy, but it does so with no description. Catching it here keeps the
            // reason in the probe's response and in the log, which is the whole value of a probe that has only just started failing.
            return HealthCheckResult.Unhealthy("The database could not be reached.", exception);
        }
    }
}
