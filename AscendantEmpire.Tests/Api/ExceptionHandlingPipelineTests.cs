using AscendantEmpire.Api.Middleware.ExceptionHandling;

using FluentAssertions;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using System.Net;
using System.Net.Http.Json;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Proves the global exception handler is actually wired into the running pipeline, over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// The lowest database tier described in <c>testing.md</c> — <b>no database at all</b>. The behaviour registered below throws before the request handler it
/// wraps is ever invoked, so nothing reaches EF Core.
/// </para>
/// <para>
/// <see cref="GlobalExceptionHandlerTests"/> covers what the handler writes; this covers the part unit tests cannot see, which is that
/// <c>UseExceptionHandler</c> is in the pipeline and reaches it. Before this ticket the same request tore the connection down with the exception, which is
/// exactly what <c>EndpointAuthorizationTests</c> used to rely on as its "got past authorization" signal.
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
    /// It is in <c>EndpointAuthorizationTests.AuthenticatedOnlyEndpoints</c>, and <c>PlayerController.GetPlayerTroopsAsync</c> does nothing before
    /// <c>mediator.Send</c> but read the game context out of the token — so a failure here can only have come from the pipeline under test.
    /// </remarks>
    private const string AuthenticatedRoute = "/player/troops";

    [Test]
    public async Task AnExceptionEscapingAHandler_IsAnsweredWithTheErrorContract_NotATornConnection()
    {
        using ApiTestFactory apiFactory = new()
        {
            // Registered after the application's own behaviours, so validation still runs first and this throws in its place of the real handler. Throwing
            // from inside the MediatR pipeline reproduces the ticket's scenario exactly: no controller wraps mediator.Send in a try/catch.
            ConfigureAdditionalTestServices = services =>
                services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ThrowingBehaviour<,>)),
        };

        using HttpClient client = apiFactory.CreateClientWithPermissions();

        // Reaching the assertions at all is half the point: until this ticket, TestServer rethrew the exception here and the await threw instead of returning.
        using HttpResponseMessage response = await client.GetAsync(AuthenticatedRoute);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        string correlationId = response.Headers.GetValues(GlobalExceptionHandler.CorrelationIdHeaderName).Single();
        ErrorBody? body = await response.Content.ReadFromJsonAsync<ErrorBody>();

        body.Should().NotBeNull();
        body!.Errors.Should().NotBeNullOrEmpty("the frontend reads this array and shows nothing useful without it");

        // The factory boots as "Testing", not Development, so this is the body a deployed instance would produce.
        body.Errors!.Should().ContainSingle();
        body.Errors[0].Should().Contain(correlationId, "the id in the body is how a user reporting a failure names it, and it must match the header");
        body.Errors[0].Should().NotContain(SentinelExceptionMessage);
    }

    /// <summary>
    /// A MediatR behaviour that always throws, standing in for any handler that fails in a way nobody anticipated.
    /// </summary>
    /// <remarks>
    /// Deliberately a behaviour rather than a substitute for <c>IMediator</c>: it throws from inside the pipeline, after routing, authorization and model
    /// binding have all succeeded, which is the only place the global handler is the thing that catches it.
    /// </remarks>
    private sealed class ThrowingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
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
