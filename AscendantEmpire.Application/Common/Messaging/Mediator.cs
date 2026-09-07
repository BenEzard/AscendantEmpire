using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Application.Common.Messaging;

/// <summary>
/// Resolves the one handler registered for a request, wraps it in the pipeline behaviours registered for it, and runs the result.
/// </summary>
/// <remarks>
/// <para>The one piece of reflection this needs is closing a dispatcher over the request's runtime type: <see cref="Send{TResponse}"/> knows the response
/// type statically but only ever sees the request as an <see cref="IRequest{TResponse}"/>, whereas the handler is registered against the concrete request
/// type. A dispatcher closed over both is what bridges the two, and everything after that point is ordinary typed code.</para>
/// <para>Those dispatchers are cached in a static dictionary, so the reflection happens once per request-and-response pair for the life of the process
/// rather than once per dispatch. The cache is static rather than per-instance because a closed dispatcher holds no state of its own — it is handed the
/// service provider on every call, so nothing about one request leaks into the next.</para>
/// </remarks>
/// <param name="services">The scope to resolve the handler and its behaviours from.</param>
internal sealed class Mediator(IServiceProvider services) : IMediator
{
    /// <summary>
    /// Dispatchers already closed over a request and response type, keyed by that pair.
    /// </summary>
    /// <remarks>
    /// Keyed by both types, not by the request alone: the same request can be dispatched through a base-typed reference as well as its own, and each of
    /// those resolves a different handler registration.
    /// </remarks>
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), object> _dispatchers = new();

    /// <inheritdoc />
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dispatcher = (RequestDispatcher<TResponse>)_dispatchers.GetOrAdd(
            (request.GetType(), typeof(TResponse)),
            static key => Activator.CreateInstance(typeof(RequestDispatcher<,>).MakeGenericType(key.RequestType, key.ResponseType))!);

        return dispatcher.Dispatch(request, services, cancellationToken);
    }

    /// <summary>
    /// The half of a dispatcher that <see cref="Send{TResponse}"/> can name — it knows the response type, but not the concrete request type.
    /// </summary>
    /// <typeparam name="TResponse">The response the request declared.</typeparam>
    private abstract class RequestDispatcher<TResponse>
    {
        /// <summary>
        /// Runs the request through its pipeline and its handler.
        /// </summary>
        /// <param name="request">The request to dispatch. Its runtime type is the one this dispatcher was closed over.</param>
        /// <param name="services">The scope to resolve the handler and its behaviours from.</param>
        /// <param name="cancellationToken">Cancels the work in flight.</param>
        /// <returns>The response produced by the pipeline and the handler.</returns>
        public abstract Task<TResponse> Dispatch(IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A dispatcher closed over both the concrete request type and its response, so the handler and behaviours can be resolved as ordinary typed services.
    /// </summary>
    /// <typeparam name="TRequest">The concrete request type, as it was registered.</typeparam>
    /// <typeparam name="TResponse">The response the request declared.</typeparam>
    private sealed class RequestDispatcher<TRequest, TResponse> : RequestDispatcher<TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <inheritdoc />
        public override Task<TResponse> Dispatch(IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;

            IRequestHandler<TRequest, TResponse> handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException(
                    $"No handler is registered for request '{typeof(TRequest).FullName}' answering '{typeof(TResponse).FullName}'. A handler is picked up "
                    + "by the assembly scan in AddMediator, so either the handler's assembly was never scanned, or the response type on the handler does "
                    + "not match the one the request declares.");

            // The innermost step: the handler itself, with the request already captured so each wrapping behaviour only has to pass the token along.
            RequestHandlerDelegate<TResponse> pipeline = token => handler.Handle(typedRequest, token);

            // Wrapped in reverse of registration order, so that the first behaviour registered ends up the outermost one and therefore runs first.
            foreach (IPipelineBehaviour<TRequest, TResponse> behaviour in services.GetServices<IPipelineBehaviour<TRequest, TResponse>>().Reverse())
            {
                // Captured into a local per iteration, because the lambda must close over the pipeline as it stood when this behaviour was wrapped, not
                // over the loop variable that keeps being reassigned.
                RequestHandlerDelegate<TResponse> next = pipeline;

                pipeline = token => behaviour.Handle(typedRequest, next, token);
            }

            return pipeline(cancellationToken);
        }
    }
}
