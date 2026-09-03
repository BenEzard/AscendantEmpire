using AscendantEmpire.Application.Common.Validation;

using MediatR;

namespace AscendantEmpire.Application.Common.Behaviours;

/// <summary>
/// Runs every validator registered for a request before its handler sees it, and answers a failure result rather than calling the handler when a rule is
/// broken.
/// </summary>
/// <remarks>
/// <para>The failure carries <see cref="ServiceResultStatus.GenericError"/>, which the API translates to HTTP 400 with the messages in the <c>errors</c>
/// array the frontend reads. A broken rule is answered, never thrown.</para>
/// <para><typeparamref name="TResponse"/> is constrained to <see cref="ServiceResultBase"/> so the failure can be built by an ordinary object initializer
/// over the properties every result shape shares. That is what removes the reflection the previous implementation needed: renaming a result type is now a
/// compile error rather than a runtime lookup that returns null and turns every 400 into a 500, and a two-argument
/// <see cref="ServiceResultBase.ServiceResult{TSuccess, TError}"/> is built correctly instead of throwing on the cast.</para>
/// <para>The constraint also decides which requests the behaviour applies to at all: the container closes an open generic only for type arguments that
/// satisfy its constraints, so a request answering anything other than a service result keeps the pipeline it had. Every validated request in the codebase
/// answers a service result, so nothing loses validation by it.</para>
/// </remarks>
/// <typeparam name="TRequest">The request being validated.</typeparam>
/// <typeparam name="TResponse">The request's response, which must be a service result so a failure can be expressed in it.</typeparam>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : ServiceResultBase, new()
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancel)
    {
        // Every validator runs, and every rule within each of them, so one response accounts for everything wrong with the request rather than only the
        // first thing. A request with no validator registered produces no messages and simply carries on.
        string[] messages = [.. validators
            .SelectMany(validator => validator.Validate(request).Errors)
            .Select(failure => failure.ErrorMessage)
            .Where(message => string.IsNullOrWhiteSpace(message) == false)];

        if (messages.Length == 0)
        {
            return await next(cancel);
        }

        return new TResponse
        {
            StatusCode = ServiceResultStatus.GenericError,
            Errors = messages
        };
    }
}
