using AscendantEmpire.Api.Middleware.ExceptionHandling;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NSubstitute;

using System.Text.Json;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Covers <see cref="GlobalExceptionHandler"/> directly, without a host.
/// </summary>
/// <remarks>
/// <para>
/// The lowest database tier described in <c>testing.md</c> — <b>no database at all</b>. The handler takes a logger and the hosting environment and touches
/// nothing else, so a <see cref="DefaultHttpContext"/> is the whole world it needs.
/// </para>
/// <para>
/// There is deliberately no test here for a cancelled request or for a response that has already started. <c>ExceptionHandlerMiddlewareImpl</c> answers both
/// of those itself — 499 for the first, a rethrow for the second — before it consults any <c>IExceptionHandler</c>, so a test asserting this class handles
/// them would pass while describing something the application never does.
/// </para>
/// </remarks>
[TestFixture]
public sealed class GlobalExceptionHandlerTests
{
    /// <summary>
    /// Stands in for the id Kestrel assigns a real request. Distinctive enough that finding it in a response body proves where it came from.
    /// </summary>
    private const string TestCorrelationId = "0HTESTCORRELATION:00000007";

    /// <summary>
    /// The message an unhandled exception carries. Every assertion about leaking checks for this string specifically.
    /// </summary>
    private const string SensitiveExceptionMessage = "Invalid column name 'PlayerSecretColumn' on table 'Game.Player'.";

    [Test]
    public async Task TryHandleAsync_AnswersWithTheErrorContract_ForAnUnhandledException()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = CreateHandler(Environments.Production, out CapturingLogger<GlobalExceptionHandler> logger);

        bool handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException(SensitiveExceptionMessage), CancellationToken.None);

        handled.Should().BeTrue("the handler is the last word on an escaped exception — returning false would hand the caller a ProblemDetails body instead");
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        httpContext.Response.ContentType.Should().StartWith("application/json");
        ReadErrors(httpContext).Should().ContainSingle();

        // Named only so the unused-variable warning does not push a reader to delete the capture the other tests rely on.
        logger.LastException.Should().BeOfType<InvalidOperationException>();
    }

    // Literals rather than Environments.Production: those are static readonly fields, which an attribute argument may not be.
    [TestCase("Production")]
    [TestCase("Testing")]
    public async Task TryHandleAsync_TellsTheCallerNothingAboutTheException_OutsideDevelopment(string environmentName)
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = CreateHandler(environmentName, out CapturingLogger<GlobalExceptionHandler> _);

        await handler.TryHandleAsync(httpContext, new InvalidOperationException(SensitiveExceptionMessage), CancellationToken.None);

        string[] errors = ReadErrors(httpContext);

        errors.Should().ContainSingle();
        errors[0].Should().Contain(TestCorrelationId, "the correlation id is the only thing that makes a reported failure traceable");
        errors[0].Should().NotContain(SensitiveExceptionMessage);
        errors[0].Should().NotContain(nameof(InvalidOperationException));
    }

    /// <remarks>
    /// "Testing" is the environment <see cref="ApiTestFactory"/> boots, so the case above is what <see cref="ExceptionHandlingPipelineTests"/> asserts over
    /// HTTP. Development is the developer's own machine, where losing the stack-trace page entirely would be a step backwards and nothing is at risk.
    /// </remarks>
    [Test]
    public async Task TryHandleAsync_AddsTheExceptionDetail_InDevelopment()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = CreateHandler(Environments.Development, out CapturingLogger<GlobalExceptionHandler> _);

        await handler.TryHandleAsync(httpContext, new InvalidOperationException(SensitiveExceptionMessage), CancellationToken.None);

        string[] errors = ReadErrors(httpContext);

        errors.Should().HaveCount(2);
        errors[0].Should().Contain(TestCorrelationId);
        errors[1].Should().Be($"{nameof(InvalidOperationException)}: {SensitiveExceptionMessage}");
    }

    [Test]
    public async Task TryHandleAsync_PutsTheCorrelationIdInTheResponseHeader()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = CreateHandler(Environments.Production, out CapturingLogger<GlobalExceptionHandler> _);

        await handler.TryHandleAsync(httpContext, new InvalidOperationException(SensitiveExceptionMessage), CancellationToken.None);

        httpContext.Response.Headers[GlobalExceptionHandler.CorrelationIdHeaderName].ToString().Should().Be(TestCorrelationId);
    }

    /// <remarks>
    /// The correlation id has to be in the rendered message, not merely attached as a property: the Serilog output template in <c>appsettings.json</c> writes
    /// <c>{Message:lj}{NewLine}{Exception}</c> and nothing else, so an id carried only as structured state would never reach the log file.
    /// </remarks>
    [Test]
    public async Task TryHandleAsync_LogsTheExceptionAsAnError_QuotingTheCorrelationId()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = CreateHandler(Environments.Production, out CapturingLogger<GlobalExceptionHandler> logger);
        InvalidOperationException exception = new(SensitiveExceptionMessage);

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        CapturedLogEntry entry = logger.Entries.Should().ContainSingle().Subject;

        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception, "the log is the only place the stack trace survives now that the response does not carry it");
        entry.Message.Should().Contain(TestCorrelationId);
        entry.Message.Should().Contain("/player/forces");
    }

    /// <remarks>
    /// A request the server could not read at all. The status and the message both come from the framework, so returning them leaks nothing, and the caller
    /// gets to know that the request was the problem rather than the server. Logged as a warning for the same reason.
    /// </remarks>
    [Test]
    public async Task TryHandleAsync_HonoursTheStatusOnABadHttpRequestException()
    {
        const string frameworkMessage = "Request body too large. The max request body size is 30000000 bytes.";

        DefaultHttpContext httpContext = CreateHttpContext();
        GlobalExceptionHandler handler = CreateHandler(Environments.Production, out CapturingLogger<GlobalExceptionHandler> logger);

        await handler.TryHandleAsync(httpContext, new BadHttpRequestException(frameworkMessage, StatusCodes.Status413PayloadTooLarge), CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        ReadErrors(httpContext).Should().ContainSingle().Which.Should().Be(frameworkMessage);
        logger.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
    }

    /// <remarks>
    /// Unreachable in the running application, since <c>TryHandleAsync</c> handles everything. It is pinned anyway because it is what the caller would get if
    /// the handler were ever unregistered, and a fallback that answered in a different shape would be worse than no fallback at all.
    /// </remarks>
    [Test]
    public async Task WriteFallbackResponseAsync_AnswersInTheSameShape()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        await GlobalExceptionHandler.WriteFallbackResponseAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        httpContext.Response.ContentType.Should().StartWith("application/json");
        ReadErrors(httpContext).Should().ContainSingle().Which.Should().Contain(TestCorrelationId);
    }

    /// <summary>
    /// Builds a handler for the given environment, handing back the logger so the caller can assert on what was written.
    /// </summary>
    private static GlobalExceptionHandler CreateHandler(string environmentName, out CapturingLogger<GlobalExceptionHandler> logger)
    {
        logger = new CapturingLogger<GlobalExceptionHandler>();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new GlobalExceptionHandler(logger, environment);
    }

    /// <summary>
    /// A request context with a writable response body, standing in for one the exception handling middleware has just cleared.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = TestCorrelationId,
        };

        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/player/forces";
        httpContext.Response.Body = new MemoryStream();

        return httpContext;
    }

    /// <summary>
    /// Reads the response back as the frontend would — parsed from JSON, by the property name on the wire.
    /// </summary>
    private static string[] ReadErrors(HttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);

        ErrorBody? body = JsonSerializer.Deserialize<ErrorBody>(httpContext.Response.Body, JsonSerializerOptions.Web);

        body.Should().NotBeNull();
        body!.Errors.Should().NotBeNull();

        return body.Errors!;
    }

    /// <summary>
    /// The wire shape of <c>IApiErrorResult</c>, declared here rather than reused so the test fails if the contract is quietly renamed.
    /// </summary>
    private sealed record ErrorBody(string[]? Errors);
}
