using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Domain.Enums.Authentication;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using System.Net.Http.Headers;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Hosts the real API in-process so tests can exercise the HTTP pipeline itself rather than calling handlers directly.
/// </summary>
/// <remarks>
/// <para>
/// This is the lowest database tier described in <c>testing.md</c> — <b>no database at all</b>. The API registers its <c>DbContextFactory</c> lazily and
/// nothing connects at startup, and authorization runs before model binding and before any handler, so a request that is rejected with 401 never reaches
/// EF Core. Tests built on this factory must not inherit <see cref="InMemoryTestBase"/> or <see cref="SqlServerTestBase"/>.
/// </para>
/// <para>
/// Configuration is supplied in memory rather than read from the developer's user-secrets, so the host boots identically on any machine and on CI. The
/// signing key matters in particular: the API deliberately throws at startup when <c>Jwt:SigningKey</c> is missing.
/// </para>
/// </remarks>
public sealed class ApiTestFactory : WebApplicationFactory<AscendantEmpire.Api.Program>
{
    /// <summary>
    /// Settings the API needs at startup, expressed the way the environment-variable provider spells them (<c>__</c> for a section separator).
    /// </summary>
    /// <remarks>
    /// These have to be environment variables rather than the usual <c>ConfigureAppConfiguration</c> callback. <c>Program.Main</c> reads
    /// <c>builder.Configuration</c> directly — to validate the signing key and build the token validation parameters — <b>before</b> it calls
    /// <c>builder.Build()</c>, and the factory's configuration callbacks do not run until after that point. <c>WebApplication.CreateBuilder</c> reads
    /// environment variables up front, so this is what arrives in time.
    /// </remarks>
    private static readonly Dictionary<string, string> RequiredStartupSettings = new()
    {
        // Anything but "Development" keeps the Swagger branch out of the pipeline, so the endpoint table a test enumerates is the one production runs.
        ["ASPNETCORE_ENVIRONMENT"] = "Testing",

        // Long enough for HMAC-SHA256; never used to verify a real token here, only to get the host past its startup check.
        ["Jwt__SigningKey"] = "test-signing-key-that-is-long-enough-for-hmac-sha256",
        ["Jwt__Issuer"] = "AscendantEmpire.Tests",
        ["Jwt__Audience"] = "AscendantEmpire.Tests",

        // Never opened deliberately — present only so the DbContext options builder has something to parse. The one-second connect timeout matters: a
        // request that slips past authorization would otherwise sit here for the default fifteen seconds before failing.
        ["ConnectionStrings__AscendantEmpireDatabase"] = "Server=(local);Database=AscendantEmpire_NotUsed;Trusted_Connection=True;Connect Timeout=1;",

        // Blank keeps the startup permission grant switched off, so the host boots without touching the database.
        ["Bootstrap__AdminUsername"] = "",

        // The API throws at startup when no CORS origin is configured, the same way it does for the signing key. This matches the base address
        // CreateAnonymousClient uses, so a test can send that origin and see the policy allow it.
        ["Cors__AllowedOrigins__0"] = "https://localhost",
    };

    /// <summary>
    /// The identity baked into test tokens. Arbitrary — nothing resolves these against a database, because authorization decides before any handler runs.
    /// </summary>
    private const int TestUserId = 1;
    private const int TestSessionId = 1;
    private const int TestGameId = 1;
    private const int TestPlayerId = 1;

    public ApiTestFactory()
    {
        // Set before the host is created, and left in place for the rest of the run: the values are inert, and re-applying them per instance is harmless.
        foreach (KeyValuePair<string, string> setting in RequiredStartupSettings)
        {
            Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }

    /// <summary>
    /// Replaces or adds registrations for a single fixture, after the application's own services have been registered.
    /// </summary>
    /// <remarks>
    /// The alternative, <c>WithWebHostBuilder</c>, hands back a plain <see cref="WebApplicationFactory{TEntryPoint}"/> and with it the two client helpers
    /// below — a fixture that swapped a service would then have to mint its own token. Set this instead, before the first client is created.
    /// </remarks>
    public Action<IServiceCollection>? ConfigureAdditionalTestServices { get; init; }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ConfigureTestServices runs last, so anything registered here wins the resolution that GetRequiredService performs.
        builder.ConfigureTestServices(services => ConfigureAdditionalTestServices?.Invoke(services));
    }

    /// <summary>
    /// Creates a client that sends no <c>Authorization</c> header, standing in for an attacker or a logged-out browser.
    /// </summary>
    /// <remarks>
    /// The base address uses <c>https</c> deliberately. <c>UseHttpsRedirection</c> sits in front of the authorization middleware, so an <c>http</c> request
    /// would come back as a redirect and every "expected 401" assertion would fail for the wrong reason. Automatic redirect following is off for the same
    /// reason: a redirect should surface as a failed assertion, not be quietly followed.
    /// </remarks>
    public HttpClient CreateAnonymousClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    /// <summary>
    /// Creates a client carrying a valid token that grants exactly the given permissions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The token is minted by the application's own <see cref="ITokenService"/> rather than hand-rolled, so it is signed with the same key, carries the same
    /// claim names, and would be rejected by the running API for exactly the reasons a real one would.
    /// </para>
    /// <para>
    /// It is deliberately a <b>game</b> token rather than a user token. Game-scoped endpoints answer a user token with their own 403 from
    /// <c>TryGetGameContext</c> ("no game entered"), which is indistinguishable at the HTTP level from a permission being refused — so a caller testing
    /// authorization would not be able to tell which rule rejected the request. Populating the game context leaves a missing permission as the only thing
    /// that can produce a 403.
    /// </para>
    /// </remarks>
    public HttpClient CreateClientWithPermissions(params PermissionType[] permissions)
    {
        ITokenService tokenService = Services.GetRequiredService<ITokenService>();

        GameContext context = new(TestUserId, "endpoint-authorization-tests", TestGameId, TestPlayerId, permissions);
        string token = tokenService.CreateGameToken(context, TestSessionId);

        HttpClient client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
