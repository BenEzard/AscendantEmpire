namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// Validates instances of <typeparamref name="T"/> against a set of declared rules.
/// </summary>
/// <remarks>
/// <para>Validation is synchronous by design. A rule that needs the database — an existence or a uniqueness check — belongs in the handler, which is where
/// every other such check in this codebase already lives and where it can be written as an ordinary EF query.</para>
/// <para>Implementations are expected to be pure and parameterless, so a validator can be constructed wherever it is needed as well as resolved from the
/// container for the MediatR pipeline.</para>
/// </remarks>
/// <typeparam name="T">The type being validated. May be a request, a DTO, an interface, or a bare primitive.</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Runs every rule against <paramref name="instance"/> and reports what failed.
    /// </summary>
    /// <param name="instance">The instance to check.</param>
    /// <returns>A result whose <see cref="ValidationResult.Errors"/> holds one entry per failure, empty when the instance is valid.</returns>
    ValidationResult Validate(T instance);
}
