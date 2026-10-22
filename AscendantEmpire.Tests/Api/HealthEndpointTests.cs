using AscendantEmpire.Application.Data;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using System.Net;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Guards the two health endpoints: that liveness answers whenever the host is up, and that readiness answers for the database rather than for itself.
/// </summary>
/// <remarks>
/// <para>
/// The distinction is the whole point of there being two, and it is the part a well-meaning change breaks quietly. Fold the database check into
/// <c>/health</c> and a database outage stops being an outage: the orchestrator reads the failing liveness probe as a sick process and restarts every
/// instance, in a loop, for a fault no restart can fix. Drop it from <c>/health/ready</c> and the opposite happens — a load balancer sends traffic to a
/// process that cannot answer a single request.
/// </para>
/// <para>
/// The main fixture needs no database, in the sense <see cref="ApiTestFactory"/> describes: the host's connection string points at a database that does
/// not exist, which is exactly the condition the readiness tests want. <see cref="ReadinessEndpoint_ReportsHealthy_WhenTheDatabaseAnswers"/> is the one
/// exception and swaps in an in-memory context factory, since there is no other way to see the healthy direction through the real pipeline.
/// </para>
/// </remarks>
[TestFixture]
public sealed class HealthEndpointTests
{
    private ApiTestFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new ApiTestFactory();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test, Description("Liveness answers 200 while the database is unreachable, so a database outage cannot put every instance into a restart loop.")]
    public async Task LivenessEndpoint_ReportsHealthy_EvenWhileTheDatabaseIsUnreachable()
    {
        using HttpClient client = _factory.CreateAnonymousClient();

        using HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "liveness must not depend on anything a container restart cannot repair");

        // Pins the minimal plaintext writer as well as the status: the endpoint is anonymous, so it must not hand an unauthenticated caller a per-check
        // breakdown of the deployment's dependencies.
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    [Test, Description("Readiness answers 503 while the database is unreachable, so a load balancer stops sending traffic the process cannot serve.")]
    public async Task ReadinessEndpoint_ReportsUnhealthy_WhenTheDatabaseIsUnreachable()
    {
        using HttpClient client = _factory.CreateAnonymousClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            "readiness runs the database check, and the test host's connection string points at a database that does not exist");
    }

    [Test, Description("Readiness answers 200 once the database answers, proving the tag predicate selects the check and the endpoint is not always failing.")]
    public async Task ReadinessEndpoint_ReportsHealthy_WhenTheDatabaseAnswers()
    {
        // A store name per run, so nothing is shared with another fixture's store.
        string storeName = $"AscendantEmpire_Health_{Guid.NewGuid():N}";

        DbContextOptions<AscendantEmpireContext> options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(storeName)
            .Options;

        // The check resolves its factory from the container, so replacing the registration is enough to make the real check see a database that answers —
        // no stub health check, which would prove only that a hard-coded Healthy comes back as a 200.
        using ApiTestFactory reachableDatabaseFactory = new()
        {
            ConfigureAdditionalTestServices = services =>
                services.AddSingleton<IDbContextFactory<AscendantEmpireContext>>(
                    new TestDbContextFactory<AscendantEmpireContext>(options)),
        };

        using HttpClient client = reachableDatabaseFactory.CreateAnonymousClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    [Test, Description("Both probes are reachable with no bearer token, which the deny-by-default fallback policy would otherwise prevent.")]
    public async Task BothEndpoints_AreReachable_WithoutAToken()
    {
        using HttpClient client = _factory.CreateAnonymousClient();

        string[] probePaths = ["/health", "/health/ready"];

        foreach (string path in probePaths)
        {
            using HttpResponseMessage response = await client.GetAsync(path);

            // A probe that has to authenticate is no probe at all: nothing polling it holds an account, and nothing could obtain one.
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "{0} must stay anonymous", path);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "{0} must stay anonymous", path);
        }
    }
}
