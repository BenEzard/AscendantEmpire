namespace AscendantEmpire.Application.Common.Messaging;

/// <summary>
/// The next step in a request's pipeline — either the behaviour wrapped inside this one, or the handler itself.
/// </summary>
/// <remarks>
/// A behaviour that calls this passes the request on; one that returns without calling it short-circuits the pipeline, and the handler never runs. That is
/// how <c>ValidationBehaviour</c> answers a broken rule without the handler ever seeing the request.
/// </remarks>
/// <typeparam name="TResponse">The response the request declared.</typeparam>
/// <param name="cancellationToken">Cancels the work in flight.</param>
/// <returns>The response produced by the rest of the pipeline.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);
