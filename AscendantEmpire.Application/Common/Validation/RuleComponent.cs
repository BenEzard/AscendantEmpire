namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// One check within a rule chain — a single operator such as <c>GreaterThan(0)</c>, together with the message it fails with.
/// </summary>
/// <remarks>
/// <para>A check comes in one of two shapes: a predicate that raises a single failure when it returns false, or a custom action that records its own
/// failures. Both live in this one type so a chain can hold them in a single list and evaluate them uniformly.</para>
/// </remarks>
/// <typeparam name="T">The type being validated.</typeparam>
/// <typeparam name="TProperty">The type of the value handed to the check.</typeparam>
internal sealed class RuleComponent<T, TProperty>
{
    private readonly Func<TProperty, bool>? _predicate;
    private readonly Action<TProperty, ValidationContext<T>>? _customCheck;

    private Func<T, string> _messageFactory;

    /// <summary>
    /// A check that raises a single failure when <paramref name="predicate"/> returns false.
    /// </summary>
    /// <param name="predicate">Returns true when the value is acceptable.</param>
    /// <param name="defaultMessage">The message to fail with until a <c>WithMessage</c> replaces it.</param>
    public RuleComponent(Func<TProperty, bool> predicate, Func<T, string> defaultMessage)
    {
        _predicate = predicate;
        _messageFactory = defaultMessage;
    }

    /// <summary>
    /// A check that records its own failures rather than answering a single yes or no.
    /// </summary>
    /// <param name="customCheck">Given the value and the run's context, records whatever it finds wrong.</param>
    public RuleComponent(Action<TProperty, ValidationContext<T>> customCheck)
    {
        _customCheck = customCheck;

        // A custom check never consults this, since it writes its failures to the context itself.
        _messageFactory = _ => string.Empty;
    }

    /// <summary>
    /// Replaces the message this check fails with. Backs <c>WithMessage</c>, which always binds to the check before it.
    /// </summary>
    /// <param name="messageFactory">Given the instance, returns the message to show the user.</param>
    public void OverrideMessage(Func<T, string> messageFactory)
    {
        _messageFactory = messageFactory;
    }

    /// <summary>
    /// Runs the check and records any failure against <paramref name="context"/>.
    /// </summary>
    /// <param name="instance">The instance being validated, which the message factory is built from.</param>
    /// <param name="value">The value this check is being run against — a property, an element of a collection, or the instance itself.</param>
    /// <param name="context">Collects whatever fails.</param>
    public void Evaluate(T instance, TProperty value, ValidationContext<T> context)
    {
        if (_customCheck is not null)
        {
            _customCheck(value, context);

            return;
        }

        if (_predicate!(value) == false)
        {
            context.AddFailure(_messageFactory(instance));
        }
    }
}
