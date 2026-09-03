using System.Linq.Expressions;

namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// Base class for a validator: declare rules in the constructor with <see cref="RuleFor"/> and <see cref="RuleForEach"/>, and the base runs them.
/// </summary>
/// <remarks>
/// <para>Rules are declared once, when the validator is constructed, and every selector is compiled at that point rather than per validation — so a
/// validator is worth constructing once and reusing, though constructing one per request costs little.</para>
/// <para>Every rule runs on every call. There is no short-circuiting, so a single <see cref="Validate"/> reports everything wrong with the instance rather
/// than only the first thing.</para>
/// </remarks>
/// <typeparam name="T">The type being validated. May be a request, a DTO, an interface, or a bare primitive.</typeparam>
public abstract class Validator<T> : IValidator<T>
{
    private readonly List<IValidationRule<T>> _rules = [];

    /// <summary>
    /// Starts a rule over one property.
    /// </summary>
    /// <remarks>
    /// <para>Pass <c>x =&gt; x</c> to write a rule over the whole instance, which is how a check spanning several properties is expressed.</para>
    /// </remarks>
    /// <param name="selector">Reads the value to check off the instance.</param>
    /// <returns>The chain to append operators to.</returns>
    protected IRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> selector)
    {
        PropertyRule<T, TProperty> rule = PropertyRule<T, TProperty>.ForSingleValue(selector.Compile(), DisplayNameFor(selector));

        _rules.Add(rule);

        return rule;
    }

    /// <summary>
    /// Starts a rule that runs over every element of a collection property.
    /// </summary>
    /// <remarks>
    /// <para>A null collection is skipped rather than failed — pair it with a <see cref="RuleFor"/> carrying <c>NotNull</c> or <c>NotEmpty</c> when the
    /// collection itself has to be present.</para>
    /// </remarks>
    /// <param name="selector">Reads the collection off the instance.</param>
    /// <returns>The chain to append operators to. Its operators see one element at a time, not the collection.</returns>
    protected IRuleBuilder<T, TElement> RuleForEach<TElement>(Expression<Func<T, IEnumerable<TElement>?>> selector)
    {
        PropertyRule<T, TElement> rule = PropertyRule<T, TElement>.ForEachElement(selector.Compile(), DisplayNameFor(selector));

        _rules.Add(rule);

        return rule;
    }

    /// <inheritdoc />
    public ValidationResult Validate(T instance)
    {
        var context = new ValidationContext<T>(instance);

        foreach (IValidationRule<T> rule in _rules)
        {
            rule.Validate(instance, context);
        }

        if (context.Failures.Count == 0)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult([.. context.Failures]);
    }

    private static string DisplayNameFor<TValue>(Expression<Func<T, TValue>> selector)
    {
        return PropertyNameFormatter.DisplayNameFor(selector, typeof(T));
    }
}
