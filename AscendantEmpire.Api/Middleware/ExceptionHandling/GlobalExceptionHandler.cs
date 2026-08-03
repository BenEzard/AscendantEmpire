using AscendantEmpire.Api.Utilities;

using Microsoft.AspNetCore.Diagnostics;

namespace AscendantEmpire.Api.Middleware.ExceptionHandling;

/// <summary>
/// Turns any exception that escapes the request pipeline into the <see cref="DTApiErrorResult"/> shape the frontend parses, and writes the exception to the
/// log against a correlation id the caller is given.
/// </summary>
/// <remarks>
/// <para>
/// Without this, an escaped exception gets the framework default: a <c>text/plain</c> 500 carrying a full stack trace in Development, and an empty 500 in
/// Production. Neither is the <c>{ errors: [] }</c> contract that <c>src/api/http.ts</c> reads, so the user was shown axios's own "Request failed with status
/// code 500" no matter what had actually gone wrong.
/// </para>
/// <para>
/// Outside Development the response carries no exception message, type or stack trace. An unhandled exception is by definition something nobody anticipated,
/// so its message is as likely to name a table, a column or a connection string as it is to help — see AE-S5-009 for what that leak looks like today. What the
/// caller gets instead is the correlation id, which is enough to find the whole exception in <c>logs/ascendant-empire-*.log</c>.
/// </para>
/// <para>
/// Registered by <c>AddExceptionHandler&lt;T&gt;</c>, which is a <b>singleton</b> registration. Only singleton dependencies may be injected here; anything
/// scoped has to be resolved from <c>httpContext.RequestServices</c> instead.
/// </para>
/// <para>
/// Two cases never reach this class, because <c>ExceptionHandlerMiddlewareImpl</c> deals with them before it consults any <see cref="IExceptionHandler"/>: a
/// request the client abandoned (answered with 499 and no body) and a response that has already started (rethrown). That is why there is no cancellation
/// branch and no <c>Response.HasStarted</c> guard below. The middleware has also just called <c>Response.Clear()</c>, so the headers are empty, nothing has
/// been written yet, and <c>httpContext.GetEndpoint()</c> is null — only the request method and path survive to be logged.
/// </para>
/// </remarks>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    /// <summary>
    /// The response header carrying the correlation id, so it can be read without parsing the body.
    /// </summary>
    /// <remarks>
    /// Browser JavaScript can only see this if the CORS policy names it in <c>WithExposedHeaders</c>, which <c>Program.cs</c> does. The id is repeated in the
    /// body regardless, because that is the part the frontend already knows how to display.
    /// </remarks>
    public const string CorrelationIdHeaderName = "X-Correlation-Id";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Kestrel's own id for the request ("0HN7A2B3C4D5E:00000003"), so an id a user quotes can be grepped straight out of the log file. Deliberately not
        // Activity.Current, which is null on every request here: nothing in this application starts a tracing activity, so that branch would never run.
        string correlationId = httpContext.TraceIdentifier;

        httpContext.Response.Headers[CorrelationIdHeaderName] = correlationId;

        if (exception is BadHttpRequestException badRequestException)
        {
            // A request the server could not read at all — a malformed JSON body, or one larger than the configured limit. The status and the message are both
            // written by the framework, so unlike everything below they are safe, and useful, to hand back. Note this type derives from IOException: when the
            // client aborted the request the middleware has already answered 499, so what reaches here is a genuinely malformed request from a live caller.
            LogRequestFailure(LogLevel.Warning, exception, "Malformed request", httpContext, correlationId);

            await WriteErrorResultAsync(httpContext, badRequestException.StatusCode, [badRequestException.Message], cancellationToken);

            return true;
        }

        LogRequestFailure(LogLevel.Error, exception, "Unhandled exception", httpContext, correlationId);

        List<string> errors = [$"An unexpected error occurred. Quote reference {correlationId} when reporting it."];

        if (environment.IsDevelopment())
        {
            // Development only. Losing the stack-trace page entirely would be a step backwards for whoever is debugging, and nothing is at risk on a
            // developer's own machine. Every other environment — including the Testing one the API tests boot — gets the opaque line above and nothing else.
            errors.Add($"{exception.GetType().Name}: {exception.Message}");
        }

        await WriteErrorResultAsync(httpContext, StatusCodes.Status500InternalServerError, errors, cancellationToken);

        return true;
    }

    /// <summary>
    /// Writes an error response in the agreed shape when this handler is bypassed.
    /// </summary>
    /// <remarks>
    /// Unreachable while <see cref="TryHandleAsync"/> returns <c>true</c> for every exception. It exists because the exception handling middleware refuses to
    /// start unless <c>ExceptionHandlerOptions</c> names a fallback of some kind, and the alternative fallback — registering <c>AddProblemDetails()</c> —
    /// would answer with <c>application/problem+json</c>, which is not the contract the frontend parses.
    /// </remarks>
    public static Task WriteFallbackResponseAsync(HttpContext httpContext)
    {
        string[] errors = [$"An unexpected error occurred. Quote reference {httpContext.TraceIdentifier} when reporting it."];

        return WriteErrorResultAsync(httpContext, StatusCodes.Status500InternalServerError, errors, httpContext.RequestAborted);
    }

    /// <summary>
    /// Logs a failed request, naming the correlation id inside the message rather than only as a property.
    /// </summary>
    /// <remarks>
    /// The Serilog output template in <c>appsettings.json</c> renders <c>{Message:lj}{NewLine}{Exception}</c> and nothing else — no <c>{Properties}</c>, no
    /// enrichers — so an id attached only as structured state would never appear in the log file a support request has to be traced through.
    /// </remarks>
    private void LogRequestFailure(LogLevel level, Exception exception, string summary, HttpContext httpContext, string correlationId)
    {
        logger.Log(level, exception, "{Summary} for {RequestMethod} {RequestPath}. Correlation id {CorrelationId}.",
            summary, httpContext.Request.Method, httpContext.Request.Path, correlationId);
    }

    /// <summary>
    /// Writes the error contract as JSON, matching what every other error response in the API produces.
    /// </summary>
    /// <remarks>
    /// <c>WriteAsJsonAsync</c> reads <c>Microsoft.AspNetCore.Http.Json.JsonOptions</c>, which is a different options object from the MVC one configured in
    /// <c>Program.cs</c>. Both camel-case their property names, so the wire shape is the same <c>{ "errors": [...] }</c>; the difference would only start to
    /// matter if the error contract ever gained an enum, since the <c>JsonStringEnumConverter</c> registered for MVC does not apply here.
    /// </remarks>
    private static Task WriteErrorResultAsync(HttpContext httpContext, int statusCode, IEnumerable<string> errors, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;

        return httpContext.Response.WriteAsJsonAsync(new DTApiErrorResult(errors), cancellationToken);
    }
}
