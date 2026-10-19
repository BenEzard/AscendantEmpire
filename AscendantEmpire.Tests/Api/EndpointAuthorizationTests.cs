using AscendantEmpire.Domain.Enums.Authentication;

using FluentAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

using System.Net;
using System.Text.RegularExpressions;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Guards how every endpoint is governed: nothing is reachable without a token unless it says so out loud, and the endpoints behind a permission are behind
/// the <i>right</i> permission.
/// </summary>
/// <remarks>
/// <para>
/// Registering authorization without a <c>FallbackPolicy</c> makes ASP.NET's default for an action with no <c>[Authorize]</c> <b>anonymous</b>, which is how
/// most of the API came to be publicly writable (AE-S2-001). These tests exist so that can never quietly happen again: adding a controller and forgetting
/// its attributes, dropping the fallback policy during a refactor, or gating an endpoint behind the wrong permission all fail here.
/// </para>
/// <para>
/// Nothing here touches a database — see <see cref="ApiTestFactory"/> for why that holds even for the request-issuing tests.
/// </para>
/// </remarks>
[TestFixture]
public sealed class EndpointAuthorizationTests
{
    /// <summary>
    /// Endpoints reachable with no token at all.
    /// </summary>
    /// <remarks>
    /// Only the three ways of obtaining a token qualify — you cannot require a token to ask for one. Anything added here is a deliberate hole in the
    /// application's front door and should be justified in review.
    /// </remarks>
    private static readonly string[] AnonymousEndpoints =
    [
        "POST /auth/login",
        "POST /auth/refresh",
        "POST /auth/register",
    ];

    /// <summary>
    /// Endpoints that require a specific permission, and which one.
    /// </summary>
    /// <remarks>
    /// This table is the specification; the <c>[Authorize(Policy = ...)]</c> attributes on the controllers are the implementation, and
    /// <see cref="PermissionGatedEndpoints_Return403_ForATokenMissingThatPermission"/> proves the two agree.
    /// </remarks>
    private static readonly Dictionary<string, PermissionType> PermissionGatedEndpoints = new()
    {
        // Player capabilities.
        ["POST /games/setup"] = PermissionType.CreateGame,
        ["POST /match-making/claim"] = PermissionType.JoinGame,
        ["PUT /match-making/games/{gameId}/my-player"] = PermissionType.JoinGame,

        // System panel: user administration and catalogue transfer.
        ["GET /auth/users"] = PermissionType.SystemPanel,
        ["POST /auth/users/inactive"] = PermissionType.SystemPanel,
        ["PUT /auth/users/permissions"] = PermissionType.SystemPanel,
        ["POST /unit-design-transfer/export"] = PermissionType.SystemPanel,
        ["GET /unit-design-transfer/export/download"] = PermissionType.SystemPanel,
        ["POST /unit-design-transfer/import"] = PermissionType.SystemPanel,

        // Deleting a game. Deliberately its own permission rather than SystemPanel: reaching the panel is a far smaller thing to grant than being able to
        // destroy a game other people are playing, so the listing that feeds the delete page is gated the same way.
        ["GET /games"] = PermissionType.DeleteGame,
        ["DELETE /games/{gameId}"] = PermissionType.DeleteGame,

        // Entity setup tool: everything that writes to the shared catalogues, plus the dev-only actions.
        ["GET /system/seed/targets"] = PermissionType.EntitySetupTool,
        ["POST /system/seed"] = PermissionType.EntitySetupTool,
        ["POST /unit-design-catalogue/bulk"] = PermissionType.EntitySetupTool,
        ["POST /unit-design-catalogue"] = PermissionType.EntitySetupTool,
        ["PUT /unit-design-catalogue/{code}"] = PermissionType.EntitySetupTool,
        ["POST /unit-design-tags"] = PermissionType.EntitySetupTool,
        ["PUT /unit-design-tags/{id}"] = PermissionType.EntitySetupTool,
        ["PUT /unit-design-tags/types"] = PermissionType.EntitySetupTool,
        ["POST /global-unit-templates"] = PermissionType.EntitySetupTool,
        ["PUT /global-unit-templates/{id}"] = PermissionType.EntitySetupTool,
        ["POST /global-unit-templates/recompile-all"] = PermissionType.EntitySetupTool,
        ["PUT /assets"] = PermissionType.EntitySetupTool,
        ["PUT /technology"] = PermissionType.EntitySetupTool,
        ["PUT /technology/experience-types"] = PermissionType.EntitySetupTool,
        ["POST /units/temporary-dev-only/spawn"] = PermissionType.EntitySetupTool,

        // The campaign-map biome and geography catalogues are edited from the Entity Setup Tool, not the map editor — the map editor places what these
        // define rather than defining it. Their reads stay open to any logged-in user, since the game page needs them to draw a tile.
        ["POST /biomes/campaign-map"] = PermissionType.EntitySetupTool,
        ["PUT /biomes/campaign-map"] = PermissionType.EntitySetupTool,
        ["POST /geography/campaign-map"] = PermissionType.EntitySetupTool,
        ["PUT /geography/campaign-map"] = PermissionType.EntitySetupTool,

        // Map editor: the map gallery mutations and the reference-data writes behind them.
        ["POST /map-gallery/add-campaign-map"] = PermissionType.MapEditor,
        ["POST /map-gallery/{id}/update"] = PermissionType.MapEditor,
        ["POST /map-gallery/{id}/update-tiles"] = PermissionType.MapEditor,
        ["PUT /map-gallery/{id}/start-positions"] = PermissionType.MapEditor,
        ["POST /map-gallery/{id}/publish"] = PermissionType.MapEditor,
        ["POST /map-gallery/{id}/export"] = PermissionType.MapEditor,
        ["GET /map-gallery/{id}/export/download"] = PermissionType.MapEditor,
        ["POST /map-gallery/import"] = PermissionType.MapEditor,
        ["PUT /map-resources/catalogue"] = PermissionType.MapEditor,
        ["POST /map-gallery/{id}/map-assets"] = PermissionType.MapEditor,
        ["PUT /map-gallery/{id}/map-assets/{placementId}"] = PermissionType.MapEditor,
        ["DELETE /map-gallery/{id}/map-assets/{placementId}"] = PermissionType.MapEditor,
        ["PUT /map-gallery/{id}/rivers"] = PermissionType.MapEditor,
        ["DELETE /map-resources/types/{code}"] = PermissionType.MapEditor,
        ["DELETE /map-resources/categories/{code}"] = PermissionType.MapEditor,
    };

    /// <summary>
    /// Endpoints any logged-in user may call.
    /// </summary>
    /// <remarks>
    /// Being here is a claim that the endpoint is safe for every account, so it covers two kinds of thing: ordinary game endpoints, which are scoped by the
    /// game context in the token rather than by a permission, and catalogue <i>reads</i> that a normal game flow needs. The reads are the subtle ones — the
    /// map gallery search and preview, for instance, are used by the game page's new-game dialog, so gating them behind the map editor permission would
    /// break game creation for everyone else.
    /// </remarks>
    private static readonly string[] AuthenticatedOnlyEndpoints =
    [
        "POST /auth/enter-game",
        "POST /auth/logout",

        "GET /biomes/campaign-map",
        "GET /geography/campaign-map",
        "GET /map-resources/catalogue",
        "GET /map-gallery/{id}/map-assets",
        "GET /assets",

        "POST /campaign-map/player",
        "POST /campaign-map/player/clear",
        "GET /campaign-map/player/tiles/{x}/{y}/sustenance",
        "GET /campaign-map/player/tiles/support",

        "GET /games/mine",
        "GET /games/settings",
        "GET /match-making/games",
        "GET /match-making/games/{gameId}/my-player",

        "GET /map-gallery/search",
        "GET /map-gallery/{id}/campaign-map",
        "GET /map-gallery/{id}/tiles",
        "GET /map-gallery/{id}/start-positions",
        "GET /map-gallery/{id}/preview",

        "GET /military/officer-ranks",
        "PUT /military/officer-ranks",
        "GET /military/organisation-tags",
        "GET /military/organisation-structures/{tagId}",
        "PUT /military/organisation-structures/{tagId}",

        "GET /player/structures",
        "GET /player/forces",
        "GET /player/game-players",
        "GET /player/assets",

        "GET /system/status",

        "GET /technology",
        "GET /technology/experience-types",
        "GET /technology/research",
        "PUT /technology/research-project",

        "POST /force-orders/move",
        "POST /force-orders/step",
        "POST /force-orders/settle",
        "GET /force-orders/actions",

        "GET /turn/campaign",
        "GET /turn/possible-actions",
        "POST /turn/end-campaign",

        "GET /intelligence/reports",
        "GET /intelligence/known-forces",
        "GET /intelligence/known-structures",

        "GET /structures/display",

        "GET /units/display",

        "GET /unit-designer/bodies",
        "GET /unit-designer/bodies/{bodyComponentId}/slots/{slotId}/components",
        "GET /unit-design-tags",
        "GET /unit-design-tags/types",
        "GET /unit-designer/templates",
        "POST /unit-designer/templates",
        "PUT /unit-designer/templates/{templateId}",
        "GET /unit-designer/templates/{templateId}/compiled",

        "GET /unit-design-catalogue",
        "GET /unit-design-catalogue/{code}",
    ];

    /// <summary>
    /// Matches a route parameter and captures its name, so <c>{templateId:int}</c> and <c>{id?}</c> both reduce to the bare name.
    /// </summary>
    private static readonly Regex RouteParameterPattern = new(@"\{([^:?}]+)[^}]*\}", RegexOptions.Compiled);

    private ApiTestFactory apiFactory = null!;

    [OneTimeSetUp]
    public void CreateFactory()
    {
        // One host for the whole fixture: booting the API is the expensive part, and none of these tests mutate its state.
        apiFactory = new ApiTestFactory();
    }

    [OneTimeTearDown]
    public void DisposeFactory()
    {
        apiFactory.Dispose();
    }

    /// <summary>
    /// The fallback policy must exist and must deny anonymous callers.
    /// </summary>
    /// <remarks>
    /// This needs its own test because a fallback policy leaves <b>no trace on the endpoints</b>: it is consulted by the authorization middleware only for
    /// endpoints that carry no <c>IAuthorizeData</c> of their own, so inspecting endpoint metadata cannot tell you whether it is configured.
    /// </remarks>
    [Test, Description("The fallback policy must exist and deny anonymous callers; it leaves no trace on endpoint metadata.")]
    public async Task FallbackPolicy_RequiresAnAuthenticatedUser()
    {
        IAuthorizationPolicyProvider policyProvider = apiFactory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy? fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();

        fallbackPolicy.Should().NotBeNull("without a fallback policy, an action with no [Authorize] attribute is reachable anonymously");
        fallbackPolicy!.Requirements.Should().ContainSingle(requirement => requirement is DenyAnonymousAuthorizationRequirement,
            "the fallback policy must require an authenticated user");
    }

    /// <summary>
    /// Every permission has a policy registered under its own name.
    /// </summary>
    /// <remarks>
    /// The controllers reference these by <c>nameof(PermissionType.X)</c>, which the compiler checks — but only that the enum member exists, not that a
    /// policy was registered for it. A missing policy surfaces at runtime as a 500 on the first request, which is a poor way to find out.
    /// </remarks>
    [Test, Description("Every permission has a policy under its own name; a missing one is a runtime 500 the compiler cannot catch.")]
    public async Task EveryPermission_HasAPolicyRegisteredUnderItsOwnName()
    {
        IAuthorizationPolicyProvider policyProvider = apiFactory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (PermissionType permission in Enum.GetValues<PermissionType>())
        {
            AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(permission.ToString());

            policy.Should().NotBeNull("{0} must have an authorization policy named after it for [Authorize(Policy = ...)] to resolve", permission);
        }
    }

    /// <summary>
    /// Every endpoint is classified exactly once, as anonymous, authenticated-only, or permission-gated.
    /// </summary>
    /// <remarks>
    /// The test that actually stops a new controller shipping ungoverned. A route that appears in none of the three lists fails, so adding an endpoint
    /// forces a deliberate decision about who may call it; a route in a list that no longer exists fails too, so the lists cannot rot.
    /// </remarks>
    [Test, Description("Every endpoint is classified exactly once, so a new controller cannot ship ungoverned and the lists cannot rot.")]
    public void EveryEndpoint_IsClassifiedExactlyOnce()
    {
        string[] actualEndpoints = [.. GetRouteEndpoints().SelectMany(DescribeEndpoint).OrderBy(description => description, StringComparer.Ordinal)];

        string[] classifiedEndpoints =
        [
            .. AnonymousEndpoints
                .Concat(AuthenticatedOnlyEndpoints)
                .Concat(PermissionGatedEndpoints.Keys)
                .OrderBy(description => description, StringComparer.Ordinal)
        ];

        classifiedEndpoints.Should().OnlyHaveUniqueItems("an endpoint that appears in two classifications has an ambiguous specification");

        actualEndpoints.Should().BeEquivalentTo(classifiedEndpoints,
            "every endpoint must be classified: anonymous, authenticated-only, or behind a named permission");
    }

    /// <summary>
    /// Every endpoint outside the anonymous list answers an unauthenticated request with 401.
    /// </summary>
    /// <remarks>
    /// The behavioural counterpart to the metadata tests, and the only one that also proves the pipeline is ordered correctly — authentication before
    /// authorization, with neither HTTPS redirection nor CORS swallowing the challenge. No request body is sent: authorization runs before model binding,
    /// so a bodyless POST is still rejected with 401 rather than 400.
    /// </remarks>
    [Test, Description("Every protected endpoint answers a tokenless request with 401, proving the pipeline is ordered correctly.")]
    public async Task EveryProtectedEndpoint_ReturnsUnauthorized_WithoutAToken()
    {
        using HttpClient anonymousClient = apiFactory.CreateAnonymousClient();

        List<string> reachableWithoutAToken = [];

        foreach ((RouteEndpoint endpoint, string httpMethod, string description) in GetClassifiableEndpoints())
        {
            if (AnonymousEndpoints.Contains(description))
            {
                continue;
            }

            HttpStatusCode? status = await SendAsync(anonymousClient, httpMethod, endpoint.RoutePattern);

            if (status != HttpStatusCode.Unauthorized)
            {
                reachableWithoutAToken.Add($"{description} returned {DescribeStatus(status)}");
            }
        }

        // Listed in the message rather than left to the assertion's own formatting, which truncates to the first item — when this breaks you want the
        // whole set of newly-public endpoints in one go, not one per re-run.
        reachableWithoutAToken.Should().BeEmpty(
            "every endpoint outside the anonymous allowlist must reject a request that carries no bearer token, but these did not:{0}{1}{0}",
            Environment.NewLine,
            string.Join(Environment.NewLine, reachableWithoutAToken));
    }

    /// <summary>
    /// The anonymous endpoints stay reachable without a token.
    /// </summary>
    /// <remarks>
    /// The other half of the story, and the one that catches an over-eager fix: a fallback policy that also caught <c>login</c> would lock every user out of
    /// the application permanently. These are sent without a body, so a 415 or 400 is the expected healthy answer — the only status that matters here is
    /// that it is not 401.
    /// </remarks>
    [Test, Description("Anonymous endpoints stay reachable without a token; gating login would lock every user out permanently.")]
    public async Task AnonymousEndpoints_AreStillReachable_WithoutAToken()
    {
        using HttpClient anonymousClient = apiFactory.CreateAnonymousClient();

        foreach (string endpointDescription in AnonymousEndpoints)
        {
            string[] parts = endpointDescription.Split(' ');

            using HttpRequestMessage request = new(new HttpMethod(parts[0]), parts[1]);
            using HttpResponseMessage response = await anonymousClient.SendAsync(request);

            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
                "{0} is how a caller obtains a token in the first place, so it cannot itself require one", endpointDescription);
        }
    }

    /// <summary>
    /// A permission-gated endpoint refuses a token that holds every permission except the one it needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Withholding exactly one permission rather than sending a bare token is what makes this precise. It fails when an endpoint is not gated at all, and
    /// equally when it is gated behind the <i>wrong</i> permission — in that case the token would happen to hold the permission actually required and the
    /// request would sail through.
    /// </para>
    /// <para>
    /// Authorization rejects these before model binding and before any handler, so no request in this test reaches the database.
    /// </para>
    /// </remarks>
    [Test, Description("Withholding exactly one permission catches an endpoint gated behind the wrong one, not just an ungated one.")]
    public async Task PermissionGatedEndpoints_Return403_ForATokenMissingThatPermission()
    {
        List<string> notGatedAsSpecified = [];

        foreach ((RouteEndpoint endpoint, string httpMethod, string description) in GetClassifiableEndpoints())
        {
            if (PermissionGatedEndpoints.TryGetValue(description, out PermissionType requiredPermission) == false)
            {
                continue;
            }

            PermissionType[] everyOtherPermission = [.. Enum.GetValues<PermissionType>().Where(permission => permission != requiredPermission)];

            using HttpClient client = apiFactory.CreateClientWithPermissions(everyOtherPermission);

            HttpStatusCode? status = await SendAsync(client, httpMethod, endpoint.RoutePattern);

            if (status != HttpStatusCode.Forbidden)
            {
                notGatedAsSpecified.Add($"{description} should require {requiredPermission} but returned {DescribeStatus(status)} without it");
            }
        }

        notGatedAsSpecified.Should().BeEmpty(
            "each endpoint must be gated behind exactly the permission the specification names, but these were not:{0}{1}{0}",
            Environment.NewLine,
            string.Join(Environment.NewLine, notGatedAsSpecified));
    }

    /// <summary>
    /// An authenticated-only endpoint accepts a token that carries no permissions at all.
    /// </summary>
    /// <remarks>
    /// Catches the opposite mistake to the test above: an endpoint quietly gated behind a permission that the specification says every logged-in user may
    /// call. A 403 is the failure; anything else — including the 500 that follows when a handler reaches for the unreachable test database — means
    /// authorization let the request through, which is all this asserts.
    /// </remarks>
    [Test, Description("An authenticated-only endpoint accepts a permissionless token, catching one quietly gated behind a permission.")]
    public async Task AuthenticatedOnlyEndpoints_AreReachable_WithATokenHoldingNoPermissions()
    {
        using HttpClient client = apiFactory.CreateClientWithPermissions();

        List<string> unexpectedlyGated = [];

        foreach ((RouteEndpoint endpoint, string httpMethod, string description) in GetClassifiableEndpoints())
        {
            if (AuthenticatedOnlyEndpoints.Contains(description) == false)
            {
                continue;
            }

            HttpStatusCode? status = await SendAsync(client, httpMethod, endpoint.RoutePattern);

            if (status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                unexpectedlyGated.Add($"{description} returned {DescribeStatus(status)} to an authenticated caller");
            }
        }

        unexpectedlyGated.Should().BeEmpty(
            "these endpoints are specified as available to any logged-in user, but rejected one:{0}{1}{0}",
            Environment.NewLine,
            string.Join(Environment.NewLine, unexpectedlyGated));
    }

    /// <summary>
    /// Issues a bodyless request to a route pattern and returns the status, or null when the request failed before a response could be read.
    /// </summary>
    /// <remarks>
    /// These tests send no request body and run against a database that does not exist, so a handler that is reached at all is expected to fail. What the
    /// caller needs to know is only that the request got <i>past</i> authorization — and since <c>GlobalExceptionHandler</c> was added, that shows up as a
    /// <b>500</b> rather than as an exception, because the pipeline now turns anything escaping a handler into a response. Each test decides for itself
    /// whether a 500 counts for or against it. The <c>catch</c> below is a transport-level backstop that should no longer fire; a null status now means
    /// something went wrong that the exception handling middleware could not answer, which is worth seeing named in a failure message.
    /// </remarks>
    private static async Task<HttpStatusCode?> SendAsync(HttpClient client, string httpMethod, RoutePattern routePattern)
    {
        using HttpRequestMessage request = new(new HttpMethod(httpMethod), BuildRequestUri(routePattern));

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request);

            return response.StatusCode;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Renders a status for a failure message, naming the "no response at all" case explicitly.
    /// </summary>
    private static string DescribeStatus(HttpStatusCode? status)
    {
        return status is null ? "no status (the request failed without a response, which the global exception handler should have prevented)" : $"{(int)status} {status}";
    }

    /// <summary>
    /// Every endpoint paired with each HTTP method it answers and the description used to classify it.
    /// </summary>
    private IEnumerable<(RouteEndpoint Endpoint, string HttpMethod, string Description)> GetClassifiableEndpoints()
    {
        foreach (RouteEndpoint endpoint in GetRouteEndpoints())
        {
            string route = NormaliseRoute(endpoint.RoutePattern.RawText);

            foreach (string httpMethod in GetHttpMethods(endpoint))
            {
                yield return (endpoint, httpMethod, $"{httpMethod} {route}");
            }
        }
    }

    /// <summary>
    /// All routable endpoints the API exposes.
    /// </summary>
    private IEnumerable<RouteEndpoint> GetRouteEndpoints()
    {
        EndpointDataSource endpointDataSource = apiFactory.Services.GetRequiredService<EndpointDataSource>();

        return endpointDataSource.Endpoints.OfType<RouteEndpoint>();
    }

    /// <summary>
    /// The HTTP methods an endpoint answers, defaulting to GET when the endpoint declares none.
    /// </summary>
    private static IReadOnlyList<string> GetHttpMethods(RouteEndpoint endpoint)
    {
        IReadOnlyList<string>? declaredMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

        return declaredMethods is { Count: > 0 } ? declaredMethods : ["GET"];
    }

    /// <summary>
    /// Renders an endpoint as one <c>"METHOD /route"</c> string per HTTP method it answers.
    /// </summary>
    private static IEnumerable<string> DescribeEndpoint(RouteEndpoint endpoint)
    {
        string route = NormaliseRoute(endpoint.RoutePattern.RawText);

        return GetHttpMethods(endpoint).Select(httpMethod => $"{httpMethod} {route}");
    }

    /// <summary>
    /// Reduces a route template to a stable form: a leading slash, and route parameters stripped of their constraints.
    /// </summary>
    /// <remarks>
    /// Without this the classification lists would have to spell out <c>{templateId:int}</c>, and adding a constraint to an existing route would break them
    /// for no reason worth flagging.
    /// </remarks>
    private static string NormaliseRoute(string? rawText)
    {
        string route = RouteParameterPattern.Replace(rawText ?? string.Empty, "{$1}");

        return "/" + route.TrimStart('/');
    }

    /// <summary>
    /// Turns a route pattern into a concrete URL by substituting <c>1</c> for every route parameter.
    /// </summary>
    /// <remarks>
    /// The value only has to get the request as far as the authorization middleware, so <c>1</c> satisfies both the <c>int</c> constraints the API uses and
    /// the unconstrained parameters.
    /// </remarks>
    private static string BuildRequestUri(RoutePattern routePattern)
    {
        string route = RouteParameterPattern.Replace(routePattern.RawText ?? string.Empty, "1");

        return "/" + route.TrimStart('/');
    }
}
