using AscendantEmpire.Api.HealthChecks;
using AscendantEmpire.Application.Data;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Covers <see cref="DatabaseHealthCheck"/> on its own: that a database which answers reports healthy, and that one which cannot be opened reports
/// unhealthy rather than throwing.
/// </summary>
/// <remarks>
/// <para>
/// The second case is the one worth pinning. <c>HealthCheckService</c> would catch an escaping exception itself and still answer 503, so the check looks
/// fine from the outside either way — but it would answer with no description, and a probe that says only "unhealthy" when it could name the reason costs
/// whoever is paged the first twenty minutes of the outage.
/// </para>
/// <para>
/// In-memory tier, per <c>docs/testing.md</c>: the check only asks whether a connection can be opened, which the in-memory provider answers without a
/// server. Whether a real SQL Server is reachable is not something any tier below a container could tell us, and <see cref="HealthEndpointTests"/> covers
/// the unreachable direction end to end.
/// </para>
/// </remarks>
[TestFixture]
public sealed class DatabaseHealthCheckTests : InMemoryTestBase
{
    [Test, Description("A database that accepts a connection reports healthy, so readiness lets traffic through once the dependency is up.")]
    public async Task CheckHealthAsync_WhenTheDatabaseAcceptsAConnection_ReportsHealthy()
    {
        DatabaseHealthCheck check = new(DbFactory);

        HealthCheckResult result = await check.CheckHealthAsync(BuildContextFor(check));

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test, Description("A factory that cannot open a context yields an unhealthy result carrying the reason, rather than an exception escaping the check.")]
    public async Task CheckHealthAsync_WhenOpeningAContextThrows_ReportsUnhealthyWithTheReason()
    {
        DatabaseHealthCheck check = new(new ThrowingDbContextFactory());

        HealthCheckResult result = await check.CheckHealthAsync(BuildContextFor(check));

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().NotBeNullOrWhiteSpace("a probe that cannot name the reason it is failing is most of the way to useless");
        result.Exception.Should().BeOfType<InvalidOperationException>("the original failure must survive into the log, not be flattened into a message");
    }

    /// <summary>
    /// Builds the context the health-check contract passes in, registering the check under the name the API uses.
    /// </summary>
    /// <remarks>
    /// <see cref="DatabaseHealthCheck"/> reads nothing from it, so this exists to satisfy the signature honestly rather than to influence the result.
    /// </remarks>
    private static HealthCheckContext BuildContextFor(DatabaseHealthCheck check)
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(DatabaseHealthCheck.CHECK_NAME, check, HealthStatus.Unhealthy, tags: null),
        };
    }

    /// <summary>
    /// A context factory that fails the way a misconfigured provider does: at the point the context is opened, not when it is resolved.
    /// </summary>
    private sealed class ThrowingDbContextFactory : IDbContextFactory<AscendantEmpireContext>
    {
        public AscendantEmpireContext CreateDbContext()
        {
            throw new InvalidOperationException("No connection string was configured.");
        }
    }
}
