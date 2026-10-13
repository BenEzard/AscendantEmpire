using AscendantEmpire.Api.Middleware.ExceptionHandling;
using AscendantEmpire.Application.Common.Behaviours;
using AscendantEmpire.Application.Common.Messaging;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Proves both exception nets are actually wired into the running pipeline, over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// The lowest database tier described in <c>testing.md</c> — <b>no database at all</b>. The behaviour registered below throws before the request handler it
/// wraps is ever invoked, so nothing reaches EF Core.
/// </para>
/// <para>
/// There are two nets, at different depths, and a request can only be caught by one of them.
/// <see cref="ExceptionHandlingBehaviour{TRequest, TResponse}"/> sits on the mediator pipeline and so catches anything raised at or below a handler;
/// <see cref="GlobalExceptionHandler"/> wraps the whole HTTP request and catches what is raised outside that pipeline — model binding, authentication, or
/// controller code itself. Each test below drives one of them, because whichever runs first is the only one that answers.
/// </para>
/// <para>
/// <see cref="GlobalExceptionHandlerTests"/> covers what the HTTP handler writes and
/// <see cref="Common.Behaviours.ExceptionHandlingBehaviourTests"/> covers what the behaviour decides; this covers the part unit tests cannot see, which is
/// that both are in the pipeline and reached. Before either existed the same request tore the connection down with the exception, which is exactly what
/// <c>EndpointAuthorizationTests</c> used to rely on as its "got past authorization" signal.
/// </para>
/// </remarks>
[TestFixture]
public sealed class ExceptionHandlingPipelineTests
{
    /// <summary>
    /// The message the deliberately-thrown exception carries. Distinctive so a test can prove it did <b>not</b> reach the caller.
    /// </summary>
    private const string SentinelExceptionMessage = "connection string Server=CoreOS\\SQLExpress leaked through the response";

    /// <summary>
    /// A route that needs a token but no permission, takes no request body, and reaches the mediator immediately.
    /// </summary>
    /// <remarks>
    /// It is in <c>EndpointAuthorizationTests.AuthenticatedOnlyEndpoints</c>, and <c>PlayerController.GetPlayerForcesAsync</c> does nothing before
    /// <c>mediator.Send</c> but read the game context out of the token — so a failure here can only have come from the pipeline under test.
    /// </remarks>
    private const string AuthenticatedRoute = "/player/forces";

    [Test, Description("An exception raised inside the mediator pipeline is answered as the error contract carrying a reference, not a torn connection.")]
    public async Task AnExceptionInsideThePipeline_IsAnsweredByTheBehaviour_WithAQuotableReference()
    {
        using ApiTestFactory apiFactory = new()
        {
            // Registered after the application's own behaviours, so this lands inside ExceptionHandlingBehaviour and throws in place of the real handler.
            ConfigureAdditionalTestServices = services =>
                services.AddTransient(typeof(IPipelineBehaviour<,>), typeof(ThrowingBehaviour<,>)),
        };

        using HttpClient client = apiFactory.CreateClientWithPermissions();

        // Reaching the assertions at all is half the point: until this was wired up, TestServer rethrew the exception here and the await threw instead.
        using HttpResponseMessage response = await client.GetAsync(AuthenticatedRoute);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError, "an unexpected exception is a 500, not the 400 a bare result defaults to");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        ErrorBody? body = await response.Content.ReadFromJsonAsync<ErrorBody>();

        body.Should().NotBeNull();
        body!.Errors.Should().NotBeNullOrEmpty("the frontend reads this array and shows nothing useful without it");

        // The factory boots as "Testing", not Development, so this is the body a deployed instance would produce.
        body.Errors!.Should().ContainSingle();
        body.Errors[0].Should().MatchRegex(@"Quote reference [0-9a-f]+ when reporting it\.", "the reference is all the user has to report the failure by");
        body.Errors[0].Should().NotContain(SentinelExceptionMessage);
    }

    [Test, Description("An exception raised outside the mediator pipeline still reaches the HTTP handler, which answers it with a correlated error contract.")]
    public async Task AnExceptionEscapingThePipeline_IsAnsweredByTheHttpHandler_NotATornConnection()
    {
        using ApiTestFactory apiFactory = new()
        {
            ConfigureAdditionalTestServices = services =>
            {
                // Stand the behaviour down for this host, so the thrown exception escapes the pipeline the way one raised in model binding, authentication or
                // controller code does. Removing it is the only way to reach the outer net from here: a behaviour registered by a test is appended, which
                // makes it the innermost one, so it can never wrap the behaviour the application registers first.
                foreach (ServiceDescriptor descriptor in services
                    .Where(service => service.ServiceType == typeof(IPipelineBehaviour<,>)
                        && service.ImplementationType == typeof(ExceptionHandlingBehaviour<,>))
                    .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddTransient(typeof(IPipelineBehaviour<,>), typeof(ThrowingBehaviour<,>));
            },
        };

        using HttpClient client = apiFactory.CreateClientWithPermissions();

        using HttpResponseMessage response = await client.GetAsync(AuthenticatedRoute);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        string correlationId = response.Headers.GetValues(GlobalExceptionHandler.CorrelationIdHeaderName).Single();
        ErrorBody? body = await response.Content.ReadFromJsonAsync<ErrorBody>();

        body.Should().NotBeNull();
        body!.Errors.Should().NotBeNullOrEmpty("the frontend reads this array and shows nothing useful without it");

        body.Errors!.Should().ContainSingle();
        body.Errors[0].Should().Contain(correlationId, "the id in the body is how a user reporting a failure names it, and it must match the header");
        body.Errors[0].Should().NotContain(SentinelExceptionMessage);
    }

    /// <summary>
    /// A pipeline behaviour that always throws, standing in for any handler that fails in a way nobody anticipated.
    /// </summary>
    /// <remarks>
    /// Deliberately a behaviour rather than a substitute for <c>IMediator</c>: it throws from inside the pipeline, after routing, authorization and model
    /// binding have all succeeded, which is where a handler's own failure would arise.
    /// </remarks>
    private sealed class ThrowingBehaviour<TRequest, TResponse> : IPipelineBehaviour<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancel)
        {
            throw new InvalidOperationException(SentinelExceptionMessage);
        }
    }

    /// <summary>
    /// The wire shape of <c>IApiErrorResult</c>, as the frontend's axios interceptor reads it.
    /// </summary>
    private sealed record ErrorBody(string[]? Errors);
}
