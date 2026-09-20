using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Sends a seed step's upsert request and turns a failure result into an exception.
/// </summary>
/// <remarks>
/// <para>
/// Every step fails the same way so the executor has one thing to catch: a handler that answers with a failed <see cref="ServiceResultBase"/> becomes an
/// <see cref="InvalidOperationException"/> naming the step and listing the handler's errors, which the executor records against the Processing row.
/// </para>
/// <para>
/// Generic over the response because a handler is resolved against the response type the caller supplies, not the request's runtime type: a request
/// declaring <c>ServiceResult&lt;T&gt;</c> cannot be dispatched through an <c>IRequest&lt;ServiceResultBase&gt;</c> parameter and still find its handler.
/// Inference keeps the call sites in the steps to a single line.
/// </para>
/// </remarks>
internal static class SeedStepDispatch
{
    /// <summary>
    /// Sends <paramref name="request"/> and throws when the handler reports failure.
    /// </summary>
    /// <typeparam name="TResponse">The result the request declared. Must be, or derive from, <see cref="ServiceResultBase"/>.</typeparam>
    /// <param name="mediator">Dispatches the request.</param>
    /// <param name="stepName">The step's name, used in the exception message so a failure says which step went wrong.</param>
    /// <param name="request">The upsert request to send.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <exception cref="InvalidOperationException">The handler returned a failure result.</exception>
    public static async Task SendAsync<TResponse>(
        IMediator mediator,
        string stepName,
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
        where TResponse : ServiceResultBase
    {
        ServiceResultBase result = await mediator.Send(request, cancellationToken);

        if (result.HasSucceeded == false)
        {
            throw new InvalidOperationException($"Seeding {stepName} failed: {string.Join("; ", result.Errors)}");
        }
    }
}
