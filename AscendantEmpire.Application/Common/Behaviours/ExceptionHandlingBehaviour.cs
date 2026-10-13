using AscendantEmpire.Application.Common.Messaging;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Common.Behaviours;

/// <summary>
/// Catches anything a handler throws, logs it against a correlation id, and answers a 500 failure result carrying that id rather than letting the exception
/// escape.
/// </summary>
/// <remarks>
/// <para>This is the one place an unexpected exception becomes a result, replacing the catch-all every handler used to carry. A handler now catches only an
/// exception it can say something useful about — a <c>DbUpdateException</c> that means a unique index was violated, say — and lets everything else go.</para>
/// <para>It sits on the mediator pipeline rather than in HTTP middleware because only one of the three routes to a handler is an HTTP request. A handler also
/// reaches another handler through <c>IMediator</c>, and <c>BackgroundProcessingService</c> reaches one through an <c>IProcessExecutor</c> with no
/// <c>HttpContext</c> in sight. A behaviour covers all three identically; middleware covers one. <c>GlobalExceptionHandler</c> in the API still wraps the
/// whole request and remains the net for everything outside a handler — model binding, authentication, controller code itself.</para>
/// <para>The failure is <b>returned, not rethrown</b>. Roughly forty handlers call another handler and branch on <see cref="ServiceResultBase.HasSucceeded"/>;
/// answering with a result keeps every one of those call sites working as written, and keeps the pipeline's contract — a request answers with its declared
/// response — true in the failure case as well as the success one.</para>
/// <para><typeparamref name="TResponse"/> is constrained to <see cref="ServiceResultBase"/> so the failure can be built by an ordinary object initializer,
/// which is also what decides where the behaviour applies at all: the container closes an open generic only over type arguments that satisfy its constraints,
/// so a request answering anything else keeps the pipeline it had and falls through to the outer net instead.</para>
/// <para>Registered ahead of <see cref="ValidationBehaviour{TRequest, TResponse}"/> in <c>ApplicationStartup</c>, which makes it the outermost behaviour, so a
/// validator that throws is caught here too.</para>
/// </remarks>
/// <typeparam name="TRequest">The request being handled.</typeparam>
/// <typeparam name="TResponse">The request's response, which must be a service result so a failure can be expressed in it.</typeparam>
public sealed class ExceptionHandlingBehaviour<TRequest, TResponse>(
    ILogger<ExceptionHandlingBehaviour<TRequest, TResponse>> logger,
    IHostEnvironment environment
) : IPipelineBehaviour<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : ServiceResultBase, new()
{
    /// <summary>
    /// How many characters of a generated GUID make up a correlation id.
    /// </summary>
    /// <remarks>
    /// Short enough to read off a screen and quote in a bug report, and still four billion values wide — far beyond anything one rolling log file will hold,
    /// which is all the uniqueness this has to provide. The id only has to find one exception among thirty days of entries.
    /// </remarks>
    private const int _CORRELATION_ID_LENGTH = 8;

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Nothing went wrong: the caller went away, or the host is shutting down. Logging this as an error would bury real faults in noise, and turning it
            // into a result would leave the background service without the signal it stops on. The guard matters — an OperationCanceledException raised while
            // our own token is still live did not come from this request being cancelled, so it falls through to the catch below and is treated as a fault.
            throw;
        }
        catch (Exception exception)
        {
            string correlationId = Guid.NewGuid().ToString("N")[.._CORRELATION_ID_LENGTH];

            // The id goes in the message template, not only in the structured state: the Serilog output template this application configures renders
            // {Message:lj} and {Exception} and nothing else, so an id attached only as a property would never reach the log file it has to be found in.
            logger.LogError(
                exception,
                "Unhandled exception handling {RequestType}. Correlation id {CorrelationId}.",
                typeof(TRequest).Name,
                correlationId);

            // Worded exactly as GlobalExceptionHandler words it, so the user is told the same thing whichever of the two nets caught the exception.
            List<string> errors = [$"An unexpected error occurred. Quote reference {correlationId} when reporting it."];

            if (environment.IsDevelopment())
            {
                // Development only, for the same reason the API's handler makes the same exception. An unanticipated exception's message is as likely to name
                // a table, a column or a connection string as it is to help, so every other environment gets the reference and nothing more.
                errors.Add($"{exception.GetType().Name}: {exception.Message}");
            }

            // StatusCode is set explicitly because a bare new() defaults to GenericError, which would answer an unexpected exception with a 400.
            return new TResponse
            {
                StatusCode = ServiceResultStatus.InternalServerError,
                Errors = errors
            };
        }
    }
}
