using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using System.Text.RegularExpressions;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Guards where an action gets the caller's identity from: the game and player must come from the token's claims, never from the request.
/// </summary>
/// <remarks>
/// <para>
/// A deny-by-default <c>FallbackPolicy</c> (see <see cref="EndpointAuthorizationTests"/>) stops an <i>anonymous</i> caller, but it does nothing about an
/// authenticated one who simply puts another player's id in the body. That was AE-S3-016: five controllers took <c>GameId</c>/<c>PlayerId</c> straight from
/// the request, so any logged-in player could end another player's turn, move their troops, or read their fog of war. The fix was to delete those parameters
/// and read <c>TryGetGameContext</c> instead — and this fixture is what stops them coming back, because re-adding one is a natural thing to do when a new
/// action needs a game id and the token is not the obvious place to look.
/// </para>
/// <para>
/// Metadata only: no request is issued and no database is touched, so this sits at the lowest tier <c>testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
public sealed class EndpointIdentityBindingTests
{
    /// <summary>
    /// Parameter and property names that name the caller's identity, and so may not be bound from a request.
    /// </summary>
    private static readonly string[] IdentityNames = ["gameId", "playerId"];

    /// <summary>
    /// Endpoints allowed to bind an identity from the request, each with the reason it is legitimate.
    /// </summary>
    /// <remarks>
    /// Every entry is a claim that the caller <i>cannot</i> be impersonating anyone by supplying the value — either because no game token exists yet at that
    /// point in the flow, or because the value is a filter rather than an identity. Anything added here should be justified in review the same way.
    /// </remarks>
    private static readonly Dictionary<string, string> AllowedIdentityBindings = new()
    {
        ["POST /auth/enter-game"] =
            "the game being entered — the caller has no game token yet, and the user id it is checked against comes from the token",

        ["POST /match-making/claim"] =
            "the slot being claimed, in a game the caller is not yet a player in; the account claiming it comes from the token",

        ["POST /games/open"] =
            "optional lobby filters on a listing reached before a game token exists, returning only lobby-visible data",

        ["POST /units/temporary-dev-only/spawn"] =
            "a developer tool behind the EntitySetupTool permission, whose purpose is placing a unit into an arbitrary game for an arbitrary player",
    };

    /// <summary>
    /// Matches a route parameter and captures its name, so <c>{templateId:int}</c> and <c>{id?}</c> both reduce to the bare name.
    /// </summary>
    private static readonly Regex RouteParameterPattern = new(@"\{([^:?}]+)[^}]*\}", RegexOptions.Compiled);

    private ApiTestFactory apiFactory = null!;

    [OneTimeSetUp]
    public void CreateFactory()
    {
        apiFactory = new ApiTestFactory();
    }

    [OneTimeTearDown]
    public void DisposeFactory()
    {
        apiFactory.Dispose();
    }

    /// <summary>
    /// No action binds a game or player id from the request, unless it is on the allowlist.
    /// </summary>
    /// <remarks>
    /// Covers the identity arriving by any route: a route value, a query string parameter, or a property on the body model — all three appear in the
    /// original finding, and all three are equally attacker-controlled.
    /// </remarks>
    [Test]
    public void NoEndpoint_BindsTheCallersIdentity_FromTheRequest()
    {
        List<string> callerSuppliedIdentities = [];

        foreach ((ControllerActionDescriptor action, string description) in GetActions())
        {
            if (AllowedIdentityBindings.ContainsKey(description))
            {
                continue;
            }

            foreach (string binding in DescribeIdentityBindings(action))
            {
                callerSuppliedIdentities.Add($"{description} binds {binding}");
            }
        }

        // Listed in the message rather than left to the assertion's own formatting, which truncates to the first item — when this breaks you want every
        // impersonatable endpoint in one go, not one per re-run.
        callerSuppliedIdentities.Should().BeEmpty(
            "the game and player must come from the caller's token via TryGetGameContext, not from the request, but these took them from the request:{0}{1}{0}",
            Environment.NewLine,
            string.Join(Environment.NewLine, callerSuppliedIdentities));
    }

    /// <summary>
    /// Every allowlist entry still names a real endpoint that still binds an identity.
    /// </summary>
    /// <remarks>
    /// Without this the allowlist rots quietly: a renamed route or a since-fixed endpoint would leave behind an exemption that silently covers nothing, and
    /// the next endpoint to land on that route would inherit it.
    /// </remarks>
    [Test]
    public void EveryAllowlistedEndpoint_StillExists_AndStillBindsAnIdentity()
    {
        Dictionary<string, string[]> bindingsByDescription = GetActions()
            .ToDictionary(entry => entry.Description, entry => DescribeIdentityBindings(entry.Action).ToArray());

        List<string> staleEntries = [];

        foreach ((string description, string reason) in AllowedIdentityBindings)
        {
            if (bindingsByDescription.TryGetValue(description, out string[]? bindings) == false)
            {
                staleEntries.Add($"{description} is allowlisted ({reason}) but no such endpoint exists");
                continue;
            }

            if (bindings.Length == 0)
            {
                staleEntries.Add($"{description} is allowlisted ({reason}) but no longer binds an identity, so the exemption can be removed");
            }
        }

        staleEntries.Should().BeEmpty(
            "an allowlist entry that covers nothing is an exemption waiting to be inherited by whatever lands on that route next:{0}{1}{0}",
            Environment.NewLine,
            string.Join(Environment.NewLine, staleEntries));
    }

    /// <summary>
    /// Every controller action paired with the <c>"METHOD /route"</c> description used to identify it.
    /// </summary>
    /// <remarks>
    /// Deliberately the same description format as <see cref="EndpointAuthorizationTests"/> uses, so an endpoint is spelled identically in both fixtures'
    /// lists and one can be compared against the other by eye.
    /// </remarks>
    private IEnumerable<(ControllerActionDescriptor Action, string Description)> GetActions()
    {
        EndpointDataSource endpointDataSource = apiFactory.Services.GetRequiredService<EndpointDataSource>();

        foreach (RouteEndpoint endpoint in endpointDataSource.Endpoints.OfType<RouteEndpoint>())
        {
            ControllerActionDescriptor? action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();

            if (action is null)
            {
                continue;
            }

            string route = NormaliseRoute(endpoint.RoutePattern.RawText);

            foreach (string httpMethod in GetHttpMethods(endpoint))
            {
                yield return (action, $"{httpMethod} {route}");
            }
        }
    }

    /// <summary>
    /// The identities an action binds from the request, rendered for a failure message (e.g. <c>"gameId (route/query)"</c> or <c>"MoveForcesDto.GameId"</c>).
    /// </summary>
    /// <remarks>
    /// Services injected with <c>[FromServices]</c> and the cancellation token are skipped: they are supplied by the framework, not by the caller. Body models
    /// are inspected one level deep, which is all the API's DTOs need — they are flat records.
    /// </remarks>
    private static IEnumerable<string> DescribeIdentityBindings(ControllerActionDescriptor action)
    {
        foreach (ParameterDescriptor parameter in action.Parameters)
        {
            if (parameter.BindingInfo?.BindingSource is { IsFromRequest: false })
            {
                continue;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            if (IdentityNames.Contains(parameter.Name, StringComparer.OrdinalIgnoreCase))
            {
                yield return $"{parameter.Name} directly";
                continue;
            }

            if (IsSimpleType(parameter.ParameterType))
            {
                continue;
            }

            foreach (PropertyInfo property in parameter.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IdentityNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    yield return $"{parameter.ParameterType.Name}.{property.Name}";
                }
            }
        }
    }

    /// <summary>
    /// Whether a type is bound as a single value rather than as a model whose properties are bound individually.
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType.IsPrimitive
            || underlyingType.IsEnum
            || underlyingType == typeof(string)
            || underlyingType == typeof(decimal)
            || underlyingType == typeof(Guid)
            || underlyingType == typeof(DateTime)
            || underlyingType == typeof(DateTimeOffset);
    }

    /// <summary>
    /// The HTTP methods an endpoint answers, defaulting to GET when it declares none.
    /// </summary>
    private static IReadOnlyList<string> GetHttpMethods(RouteEndpoint endpoint)
    {
        IReadOnlyList<string>? declaredMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

        return declaredMethods is { Count: > 0 } ? declaredMethods : ["GET"];
    }

    /// <summary>
    /// Reduces a route template to a stable form: a leading slash, and route parameters stripped of their constraints.
    /// </summary>
    private static string NormaliseRoute(string? template)
    {
        string route = RouteParameterPattern.Replace(template ?? string.Empty, "{$1}");

        return "/" + route.TrimStart('/');
    }
}
