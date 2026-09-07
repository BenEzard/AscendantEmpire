namespace AscendantEmpire.Application.Common.Messaging;

/// <summary>
/// Wraps the handling of a request, so cross-cutting work can run either side of the handler without every handler repeating it.
/// </summary>
/// <remarks>
/// <para>Behaviours are registered as an open generic — <c>services.AddTransient(typeof(IPipelineBehaviour&lt;,&gt;), typeof(MyBehaviour&lt;,&gt;))</c> —
/// and the container closes them per request. Any generic constraints an implementation declares therefore decide which requests it applies to at all: the
/// container closes an open generic only over type arguments that satisfy its constraints, and passes over the rest rather than throwing.</para>
/// <para>Every behaviour registered for a request runs, in registration order, with the first registered outermost. A behaviour that does not call
/// <paramref name="next"/> stops the pipeline there and the handler is never reached.</para>
/// </remarks>
/// <typeparam name="TRequest">The request being handled.</typeparam>
/// <typeparam name="TResponse">The response the request declared.</typeparam>
public interface IPipelineBehaviour<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Runs this step of the pipeline, calling <paramref name="next"/> to pass the request on.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">The rest of the pipeline — the behaviour inside this one, or the handler.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>The response to answer the request with.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
