namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// The outcome of running an <see cref="IValidator{T}"/> over one instance.
/// </summary>
/// <remarks>
/// <para>Every rule in a validator runs — there is no short-circuiting once something fails — so <see cref="Errors"/> holds every failure the instance
/// produced, in the order the rules that produced them were declared.</para>
/// </remarks>
public sealed class ValidationResult
{
    /// <summary>
    /// The shared result for an instance that broke no rules.
    /// </summary>
    /// <remarks>
    /// <para>Safe to share because the type is immutable and its failure list is empty. Validation succeeds on the overwhelming majority of requests, so
    /// handing back the same instance saves an allocation on the common path.</para>
    /// </remarks>
    public static ValidationResult Success { get; } = new([]);

    /// <summary>
    /// Builds a result over the failures a validator collected.
    /// </summary>
    /// <param name="errors">The failures, in the order the rules that produced them were declared.</param>
    public ValidationResult(IReadOnlyList<ValidationFailure> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// Every failure the instance produced, in rule declaration order. Empty when the instance is valid.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Errors { get; }

    /// <summary>
    /// True when the instance broke no rules.
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
