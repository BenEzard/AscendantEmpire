using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// Finds the validators in an assembly and registers them, so a new validator needs no wiring of its own.
/// </summary>
public static class ValidatorRegistration
{
    /// <summary>
    /// Registers every concrete <see cref="IValidator{T}"/> in <paramref name="assembly"/> against the type it validates.
    /// </summary>
    /// <remarks>
    /// <para>Internal types are included, because every validator in this codebase is internal — declared beside the request it validates rather than
    /// exposed.</para>
    /// <para>Validators are registered as singletons. Each one compiles its rule selectors when it is constructed, so a shorter lifetime would recompile
    /// every selector on every request; and a singleton is safe because a validator holds no state beyond the rules it was built with. That safety is what
    /// the parameterless-constructor check below enforces — a validator that took a dependency could not honour it.</para>
    /// <para>Abstract types and open generic definitions are skipped. An open generic has no type to validate until it is closed, so it is the closed
    /// subclass that gets registered.</para>
    /// </remarks>
    /// <param name="services">The container to register into.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <exception cref="InvalidOperationException">A validator was found that cannot be constructed without arguments.</exception>
    public static IServiceCollection AddValidatorsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (Type validatorType in assembly.GetTypes().Where(IsConcreteType))
        {
            Type[] validatedInterfaces = [.. validatorType.GetInterfaces().Where(IsValidatorInterface)];

            if (validatedInterfaces.Length == 0)
            {
                continue;
            }

            if (validatorType.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"Validator '{validatorType.FullName}' cannot be constructed without arguments. Validators are shared singletons and must hold no "
                    + "dependencies — a rule that needs the database belongs in the handler, where it can be written as an ordinary query.");
            }

            foreach (Type validatedInterface in validatedInterfaces)
            {
                services.AddSingleton(validatedInterface, validatorType);
            }
        }

        return services;
    }

    /// <summary>
    /// Whether a type is one that could be constructed and registered as it stands.
    /// </summary>
    private static bool IsConcreteType(Type type)
    {
        return type.IsClass && type.IsAbstract == false && type.IsGenericTypeDefinition == false;
    }

    /// <summary>
    /// Whether an interface is a closed <see cref="IValidator{T}"/>.
    /// </summary>
    private static bool IsValidatorInterface(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IValidator<>);
    }
}
