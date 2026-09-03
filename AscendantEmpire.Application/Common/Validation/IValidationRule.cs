namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// One <c>RuleFor</c> or <c>RuleForEach</c> chain, as the validator that owns it sees it.
/// </summary>
/// <remarks>
/// <para>Exists so <see cref="Validator{T}"/> can hold rules over differently-typed properties in a single list — the property type is closed over by the
/// implementation and does not appear here.</para>
/// </remarks>
/// <typeparam name="T">The type being validated.</typeparam>
internal interface IValidationRule<T>
{
    /// <summary>
    /// Runs every check in the chain against <paramref name="instance"/>, recording failures on <paramref name="context"/>.
    /// </summary>
    /// <param name="instance">The instance to check.</param>
    /// <param name="context">Collects whatever fails.</param>
    void Validate(T instance, ValidationContext<T> context);
}
