namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// A rule chain over one property: the values to check, the checks to run, and an optional condition gating the lot.
/// </summary>
/// <remarks>
/// <para>Backs both <c>RuleFor</c> and <c>RuleForEach</c>. The difference between them is only how many values the rule yields — one for a plain property,
/// one per element for a collection — so both are expressed by the same selector shape and share every other behaviour.</para>
/// <para>The type is its own builder: <see cref="Validator{T}"/> hands the caller back the rule it just created, and each operator appended to the returned
/// chain mutates that rule in place.</para>
/// </remarks>
/// <typeparam name="T">The type being validated.</typeparam>
/// <typeparam name="TProperty">The type of the value each check is handed.</typeparam>
internal sealed class PropertyRule<T, TProperty> : IValidationRule<T>, IRuleBuilder<T, TProperty>
{
    private readonly Func<T, IEnumerable<TProperty>> _valuesToCheck;
    private readonly List<RuleComponent<T, TProperty>> _components = [];

    private Func<T, bool>? _condition;

    private PropertyRule(Func<T, IEnumerable<TProperty>> valuesToCheck, string displayName)
    {
        _valuesToCheck = valuesToCheck;
        DisplayName = displayName;
    }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <summary>
    /// Builds a rule over a single property value.
    /// </summary>
    /// <param name="selector">Reads the value to check off the instance.</param>
    /// <param name="displayName">The property's display name, used to build default messages.</param>
    public static PropertyRule<T, TProperty> ForSingleValue(Func<T, TProperty> selector, string displayName)
    {
        return new PropertyRule<T, TProperty>(instance => [selector(instance)], displayName);
    }

    /// <summary>
    /// Builds a rule that runs over every element of a collection property.
    /// </summary>
    /// <remarks>
    /// <para>A null collection yields no values, so the rule simply does not fire — it is not itself a failure. A collection that must be present is paired
    /// with a <c>NotNull</c> or <c>NotEmpty</c> rule of its own, which is how the existing validators are written.</para>
    /// </remarks>
    /// <param name="selector">Reads the collection off the instance.</param>
    /// <param name="displayName">The property's display name, used to build default messages.</param>
    public static PropertyRule<T, TProperty> ForEachElement(Func<T, IEnumerable<TProperty>?> selector, string displayName)
    {
        return new PropertyRule<T, TProperty>(instance => selector(instance) ?? [], displayName);
    }

    /// <inheritdoc />
    public IRuleBuilder<T, TProperty> AddCheck(Func<TProperty, bool> predicate, Func<T, string> defaultMessage)
    {
        _components.Add(new RuleComponent<T, TProperty>(predicate, defaultMessage));

        return this;
    }

    /// <inheritdoc />
    public IRuleBuilder<T, TProperty> AddCustomCheck(Action<TProperty, ValidationContext<T>> check)
    {
        _components.Add(new RuleComponent<T, TProperty>(check));

        return this;
    }

    /// <inheritdoc />
    public IRuleBuilder<T, TProperty> WithMessage(string message)
    {
        return WithMessage(_ => message);
    }

    /// <inheritdoc />
    public IRuleBuilder<T, TProperty> WithMessage(Func<T, string> messageFactory)
    {
        if (_components.Count == 0)
        {
            throw new InvalidOperationException($"WithMessage was called on the rule for '{DisplayName}' before any check it could apply to.");
        }

        // WithMessage binds to the check immediately before it, not to the whole chain. So in .NotEmpty().MaximumLength(50).WithMessage("...") the message
        // belongs to MaximumLength alone and NotEmpty keeps its default — which is what the validators written against FluentValidation expect.
        _components[^1].OverrideMessage(messageFactory);

        return this;
    }

    /// <inheritdoc />
    public IRuleBuilder<T, TProperty> When(Func<T, bool> condition)
    {
        _condition = condition;

        return this;
    }

    /// <inheritdoc />
    public void Validate(T instance, ValidationContext<T> context)
    {
        if (_condition is not null && _condition(instance) == false)
        {
            return;
        }

        foreach (TProperty value in _valuesToCheck(instance))
        {
            foreach (RuleComponent<T, TProperty> component in _components)
            {
                component.Evaluate(instance, value, context);
            }
        }
    }
}
