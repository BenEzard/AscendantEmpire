using AscendantEmpire.Api.Middleware.ExceptionHandling;

using FluentAssertions;

using System.Net;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Guards the <c>AllowReactApp</c> policy end to end: that the origins configuration supplies are the ones the running pipeline actually allows.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CorsSettingsTests"/> covers the settings object in isolation; these tests cover the wiring either side of it, which is the part a refactor
/// is most likely to break quietly. A policy built from an empty list, or from a list the host never read, answers every preflight without an
/// <c>Access-Control-Allow-Origin</c> header — and the API itself looks perfectly healthy while the browser refuses every call.
/// </para>
/// <para>
/// The allowed origin is <c>https://localhost</c>, matching the base address <see cref="ApiTestFactory"/> hands its clients; see the <c>Cors</c> entry in
/// the factory's startup settings.
/// </para>
/// <para>
/// No database: a preflight is answered by the CORS middleware before authorization, and the one real request below reads configuration only. This is the
/// lowest tier <c>docs/testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
public sealed class CorsPolicyTests
{
    /// <summary>The origin the host is configured to allow, repeated here so a test failure names what it expected.</summary>
    private const string _ALLOWED_ORIGIN = "https://localhost";

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

    [Test, Description("A preflight from the configured origin is allowed, proving the host reads the origin list rather than a literal.")]
    public async Task Preflight_FromTheConfiguredOrigin_IsAllowed()
    {
        HttpClient client = _factory.CreateAnonymousClient();

        HttpResponseMessage response = await client.SendAsync(BuildPreflight("/games/settings", _ALLOWED_ORIGIN));

        // The CORS middleware answers a preflight itself, before authentication runs — which is why an anonymous client is enough here.
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(_ALLOWED_ORIGIN);
    }

    [Test, Description("A preflight from any other origin gets no allow header, so widening the list stays the only way to admit a new front end.")]
    public async Task Preflight_FromAnUnconfiguredOrigin_IsNotAllowed()
    {
        HttpClient client = _factory.CreateAnonymousClient();

        HttpResponseMessage response = await client.SendAsync(BuildPreflight("/games/settings", "https://not-the-frontend.example.com"));

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Test, Description("A real cross-origin response carries the allow header and exposes the correlation id, the two things the frontend needs to read it.")]
    public async Task Request_FromTheConfiguredOrigin_CarriesTheAllowAndExposeHeaders()
    {
        HttpClient client = _factory.CreateClientWithPermissions();
        client.DefaultRequestHeaders.Add("Origin", _ALLOWED_ORIGIN);

        HttpResponseMessage response = await client.GetAsync("/games/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(_ALLOWED_ORIGIN);

        // Pins the WithExposedHeaders line in Program.cs: without it the correlation id on an error response is unreadable from JavaScript.
        response.Headers.GetValues("Access-Control-Expose-Headers").Should().Contain(GlobalExceptionHandler.CorrelationIdHeaderName);
    }

    /// <summary>
    /// Builds the request a browser sends ahead of a cross-origin call: an <c>OPTIONS</c> carrying the origin and the method it intends to use.
    /// </summary>
    private static HttpRequestMessage BuildPreflight(string path, string origin)
    {
        HttpRequestMessage request = new(HttpMethod.Options, path);

        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        return request;
    }
}
