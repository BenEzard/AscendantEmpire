namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// Collects the failures raised while a validator runs, and is handed to <c>Custom</c> rules so they can add failures of their own.
/// </summary>
/// <remarks>
/// <para>A <c>Custom</c> rule is the way to raise several failures from a single check — every built-in operator raises at most one.</para>
/// </remarks>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class ValidationContext<T>
{
    private readonly List<ValidationFailure> _failures = [];

    /// <summary>
    /// Starts a context for one validation run.
    /// </summary>
    /// <param name="instanceToValidate">The instance the rules will be run against.</param>
    public ValidationContext(T instanceToValidate)
    {
        InstanceToValidate = instanceToValidate;
    }

    /// <summary>
    /// The instance currently being validated.
    /// </summary>
    public T InstanceToValidate { get; }

    /// <summary>
    /// Every failure recorded so far, in the order it was added.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures => _failures;

    /// <summary>
    /// Records a failure. A single <c>Custom</c> rule may call this any number of times, or not at all.
    /// </summary>
    /// <param name="errorMessage">The message to show the user.</param>
    public void AddFailure(string errorMessage)
    {
        _failures.Add(new ValidationFailure(errorMessage));
    }
}
