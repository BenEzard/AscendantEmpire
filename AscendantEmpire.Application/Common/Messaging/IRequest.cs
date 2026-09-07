namespace AscendantEmpire.Application.Common.Messaging;

/// <summary>
/// Marks a type as a request that is dispatched to exactly one handler and answered with a <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// <para>The interface declares no members. All it does is tie a request type to the response its handler produces, which is what lets
/// <see cref="IMediator.Send{TResponse}"/> be called without naming the handler and still be checked by the compiler at the call site.</para>
/// <para><typeparamref name="TResponse"/> is covariant so a caller holding a request through a base-typed reference — a seeding step that takes an
/// <c>IRequest&lt;ServiceResultBase&gt;</c>, say — can dispatch it without knowing the more specific result the handler really returns. The handler is
/// resolved against the response type the caller supplies, so a request meant to be dispatched that way must declare the base response type itself rather
/// than relying on the conversion.</para>
/// </remarks>
/// <typeparam name="TResponse">What a handler for this request answers with.</typeparam>
public interface IRequest<out TResponse>;
