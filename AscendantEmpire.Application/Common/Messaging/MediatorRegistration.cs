using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AscendantEmpire.Application.Common.Messaging;

/// <summary>
/// Finds the request handlers in an assembly and registers them, so a new handler needs no wiring of its own.
/// </summary>
public static class MediatorRegistration
{
    /// <summary>
    /// Registers <see cref="IMediator"/> and every concrete <see cref="IRequestHandler{TRequest, TResponse}"/> in <paramref name="assembly"/>.
    /// </summary>
    /// <remarks>
    /// <para>Internal types are included, because nearly every handler in this codebase is internal — declared beside the request it answers rather than
    /// exposed. Abstract types and open generic definitions are skipped: neither can be constructed as it stands, so it is the closed subclass that gets
    /// registered.</para>
    /// <para>A request may have only one handler, and that is enforced here rather than left to the container. Registering a second implementation against
    /// the same request would not fail — the container would simply hand out the last one registered — so which handler answers a request would come down
    /// to the order the assembly happened to be scanned in. Failing at startup instead makes an accidental duplicate impossible to miss.</para>
    /// <para>Handlers are registered as transients. They are cheap objects built per dispatch and they hold no state between requests, and a transient may
    /// safely take the scoped dependencies — a DbContext factory, the mediator itself — that handlers are built from.</para>
    /// <para>Calling this twice over the same assembly is harmless: a handler already registered against the same implementation is passed over rather
    /// than counted as a duplicate.</para>
    /// </remarks>
    /// <param name="services">The container to register into.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">Two handlers in scope claim the same request.</exception>
    public static IServiceCollection AddMediator(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        services.TryAddTransient<IMediator, Mediator>();

        // Seeded from what is already registered rather than starting empty, so the one-handler rule holds across separate calls as well as within one.
        Dictionary<Type, Type> handlersByRequest = ExistingHandlers(services);

        foreach (Type handlerType in assembly.GetTypes().Where(IsConcreteType))
        {
            foreach (Type handlerInterface in handlerType.GetInterfaces().Where(IsRequestHandlerInterface))
            {
                if (handlersByRequest.TryGetValue(handlerInterface, out Type? alreadyRegistered))
                {
                    if (alreadyRegistered == handlerType)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Both '{alreadyRegistered.FullName}' and '{handlerType.FullName}' handle '{handlerInterface}'. A request ties back to exactly one "
                        + "handler — delete or rename one of them, or give the second its own request.");
                }

                handlersByRequest[handlerInterface] = handlerType;
                services.AddTransient(handlerInterface, handlerType);
            }
        }

        return services;
    }

    /// <summary>
    /// The handler registrations already in the container, keyed by the closed handler interface they answer.
    /// </summary>
    /// <remarks>
    /// A registration made by hand rather than by a scan — a factory or an instance, with no implementation type to name — still occupies its request, so
    /// it is recorded against the interface itself. That reads back as a duplicate if a scan later finds a handler for the same request, which is the
    /// right answer: the hand-written registration would otherwise be silently replaced.
    /// </remarks>
    private static Dictionary<Type, Type> ExistingHandlers(IServiceCollection services)
    {
        Dictionary<Type, Type> handlersByRequest = [];

        foreach (ServiceDescriptor descriptor in services.Where(descriptor => IsRequestHandlerInterface(descriptor.ServiceType)))
        {
            handlersByRequest[descriptor.ServiceType] = descriptor.ImplementationType ?? descriptor.ServiceType;
        }

        return handlersByRequest;
    }

    /// <summary>
    /// Whether a type is one that could be constructed and registered as it stands.
    /// </summary>
    private static bool IsConcreteType(Type type)
    {
        return type.IsClass && type.IsAbstract == false && type.IsGenericTypeDefinition == false;
    }

    /// <summary>
    /// Whether an interface is a closed <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    private static bool IsRequestHandlerInterface(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
    }
}
