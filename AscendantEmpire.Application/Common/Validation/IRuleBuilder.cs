namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// The fluent chain returned by <c>RuleFor</c> and <c>RuleForEach</c>, onto which checks are appended.
/// </summary>
/// <remarks>
/// <para>The members here are the extension point that the operators in <see cref="RuleBuilderExtensions"/> are built on. Validators call those operators —
/// <c>GreaterThan</c>, <c>NotEmpty</c>, <c>Must</c> and the rest — rather than these members directly.</para>
/// <para><typeparamref name="TProperty"/> is covariant so that an operator declared for a nullable property type (<c>string?</c>, say) also accepts a chain
/// built from the non-nullable one. That keeps the operator set to a single declaration per property type instead of one per nullability.</para>
/// </remarks>
/// <typeparam name="T">The type being validated.</typeparam>
/// <typeparam name="TProperty">The type of the value each check in this chain is handed.</typeparam>
public interface IRuleBuilder<T, out TProperty>
{
    /// <summary>
    /// The property's display name, used to build a check's message when it is not given an explicit one.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Appends a check that raises one failure when <paramref name="predicate"/> returns false.
    /// </summary>
    /// <param name="predicate">Returns true when the value is acceptable.</param>
    /// <param name="defaultMessage">Builds the failure message from the instance being validated. Replaced by any <c>WithMessage</c> that follows.</param>
    IRuleBuilder<T, TProperty> AddCheck(Func<TProperty, bool> predicate, Func<T, string> defaultMessage);

    /// <summary>
    /// Appends a check that writes its own failures — none, one, or many — straight to the context.
    /// </summary>
    /// <param name="check">Given the value and the run's context, records whatever it finds wrong.</param>
    IRuleBuilder<T, TProperty> AddCustomCheck(Action<TProperty, ValidationContext<T>> check);

    /// <summary>
    /// Replaces the message of the check immediately before it in the chain.
    /// </summary>
    /// <param name="message">The message to show the user.</param>
    IRuleBuilder<T, TProperty> WithMessage(string message);

    /// <summary>
    /// Replaces the message of the check immediately before it in the chain, building it from the instance being validated.
    /// </summary>
    /// <param name="messageFactory">Given the instance, returns the message to show the user. Called only when the check fails.</param>
    IRuleBuilder<T, TProperty> WithMessage(Func<T, string> messageFactory);

    /// <summary>
    /// Gates every check in this chain on <paramref name="condition"/>.
    /// </summary>
    /// <remarks>
    /// <para>The condition is rule-level: it applies to the whole chain however it is ordered, so it reads the same whether it is written before or after
    /// the <c>WithMessage</c> it sits beside. A chain that needs a different condition is written as its own <c>RuleFor</c>.</para>
    /// </remarks>
    /// <param name="condition">Given the instance, returns true when the chain's checks should run.</param>
    IRuleBuilder<T, TProperty> When(Func<T, bool> condition);
}
