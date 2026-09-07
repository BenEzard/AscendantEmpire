namespace AscendantEmpire.Application.Common.Messaging;

/// <summary>
/// Dispatches a request to its one handler, through whatever pipeline behaviours are registered for it.
/// </summary>
/// <remarks>
/// This is what a controller or a background executor depends on instead of the handler itself, so a caller names the work it wants done rather than the
/// class that does it.
/// </remarks>
public interface IMediator
{
    /// <summary>
    /// Sends a request to its handler and returns what the handler answered.
    /// </summary>
    /// <remarks>
    /// The handler is resolved from the container against the request's runtime type and <typeparamref name="TResponse"/>, so a request dispatched through
    /// a base-typed reference must declare that base response type — see <see cref="IRequest{TResponse}"/>.
    /// </remarks>
    /// <typeparam name="TResponse">The response the request declared.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>The response produced by the pipeline and the handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the request.</exception>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
