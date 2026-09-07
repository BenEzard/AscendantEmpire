namespace AscendantEmpire.Application.Common.Messaging;

/// <summary>
/// Handles one request type, and is the only handler that may do so.
/// </summary>
/// <remarks>
/// <para>Handlers are discovered by <see cref="MediatorRegistration.AddMediator"/> scanning the assembly, so a new handler needs no wiring of its own —
/// declaring this interface is the whole registration. That scan is also what enforces the one-handler rule: a second implementation claiming the same
/// request fails at startup rather than quietly shadowing the first.</para>
/// <para>Implementations are resolved once per dispatch, so a handler may take scoped dependencies such as a DbContext factory.</para>
/// </remarks>
/// <typeparam name="TRequest">The request this handler answers.</typeparam>
/// <typeparam name="TResponse">The response that request declares.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Does the work the request asks for.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>The response the request declared.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
