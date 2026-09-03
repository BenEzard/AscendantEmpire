using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// The operators a rule chain is built from — <c>NotEmpty</c>, <c>GreaterThan</c>, <c>Must</c> and the rest.
/// </summary>
/// <remarks>
/// <para>Deliberately narrow: it carries the operators the validators in this codebase actually use and nothing else. An operator that is wanted later is
/// cheap to add here, and adding one is the only place it needs to be added.</para>
/// <para>Each operator supplies a default message so that a rule written without <c>WithMessage</c> still says something intelligible. Only one rule in the
/// codebase leans on a default, and the wording of that one — <see cref="NotEmpty"/> — matches what FluentValidation produced. The other defaults read
/// close to FluentValidation's without reproducing the parts that quoted the value the caller supplied.</para>
/// </remarks>
public static class RuleBuilderExtensions
{
    private const int _REGEX_TIMEOUT_SECONDS = 2;

    /// <summary>
    /// Fails when the value is null. A non-nullable value type always passes.
    /// </summary>
    public static IRuleBuilder<T, TProperty> NotNull<T, TProperty>(this IRuleBuilder<T, TProperty> rule)
    {
        return rule.AddCheck(value => value is not null, _ => $"'{rule.DisplayName}' must not be empty.");
    }

    /// <summary>
    /// Fails when the value is null, a blank string, an empty collection, or the type's default.
    /// </summary>
    public static IRuleBuilder<T, TProperty> NotEmpty<T, TProperty>(this IRuleBuilder<T, TProperty> rule)
    {
        return rule.AddCheck(IsNotEmpty, _ => $"'{rule.DisplayName}' must not be empty.");
    }

    /// <summary>
    /// Fails when the value is not strictly greater than <paramref name="threshold"/>. A null value fails.
    /// </summary>
    public static IRuleBuilder<T, TProperty> GreaterThan<T, TProperty>(this IRuleBuilder<T, TProperty> rule, TProperty threshold)
        where TProperty : IComparable<TProperty>
    {
        return rule.AddCheck(
            value => value is not null && value.CompareTo(threshold) > 0,
            _ => $"'{rule.DisplayName}' must be greater than '{threshold}'.");
    }

    /// <summary>
    /// Fails when the value is absent or not strictly greater than <paramref name="threshold"/>.
    /// </summary>
    public static IRuleBuilder<T, TProperty?> GreaterThan<T, TProperty>(this IRuleBuilder<T, TProperty?> rule, TProperty threshold)
        where TProperty : struct, IComparable<TProperty>
    {
        return rule.AddCheck(
            value => value.HasValue && value.Value.CompareTo(threshold) > 0,
            _ => $"'{rule.DisplayName}' must be greater than '{threshold}'.");
    }

    /// <summary>
    /// Fails when the value is below <paramref name="threshold"/>. A null value fails.
    /// </summary>
    public static IRuleBuilder<T, TProperty> GreaterThanOrEqualTo<T, TProperty>(this IRuleBuilder<T, TProperty> rule, TProperty threshold)
        where TProperty : IComparable<TProperty>
    {
        return rule.AddCheck(
            value => value is not null && value.CompareTo(threshold) >= 0,
            _ => $"'{rule.DisplayName}' must be greater than or equal to '{threshold}'.");
    }

    /// <summary>
    /// Fails when the value is absent or below <paramref name="threshold"/>.
    /// </summary>
    public static IRuleBuilder<T, TProperty?> GreaterThanOrEqualTo<T, TProperty>(this IRuleBuilder<T, TProperty?> rule, TProperty threshold)
        where TProperty : struct, IComparable<TProperty>
    {
        return rule.AddCheck(
            value => value.HasValue && value.Value.CompareTo(threshold) >= 0,
            _ => $"'{rule.DisplayName}' must be greater than or equal to '{threshold}'.");
    }

    /// <summary>
    /// Fails when the value falls outside <paramref name="from"/> to <paramref name="to"/>, both ends included. A null value fails.
    /// </summary>
    public static IRuleBuilder<T, TProperty> InclusiveBetween<T, TProperty>(this IRuleBuilder<T, TProperty> rule, TProperty from, TProperty to)
        where TProperty : IComparable<TProperty>
    {
        return rule.AddCheck(
            value => value is not null && value.CompareTo(from) >= 0 && value.CompareTo(to) <= 0,
            _ => $"'{rule.DisplayName}' must be between {from} and {to}.");
    }

    /// <summary>
    /// Fails when the value is absent or falls outside <paramref name="from"/> to <paramref name="to"/>, both ends included.
    /// </summary>
    public static IRuleBuilder<T, TProperty?> InclusiveBetween<T, TProperty>(this IRuleBuilder<T, TProperty?> rule, TProperty from, TProperty to)
        where TProperty : struct, IComparable<TProperty>
    {
        return rule.AddCheck(
            value => value.HasValue && value.Value.CompareTo(from) >= 0 && value.Value.CompareTo(to) <= 0,
            _ => $"'{rule.DisplayName}' must be between {from} and {to}.");
    }

    /// <summary>
    /// Fails when the string is longer than <paramref name="maximumLength"/>. A null string passes — pair with <see cref="NotEmpty"/> when it must be there.
    /// </summary>
    public static IRuleBuilder<T, string?> MaximumLength<T>(this IRuleBuilder<T, string?> rule, int maximumLength)
    {
        return rule.AddCheck(
            value => value is null || value.Length <= maximumLength,
            _ => $"The length of '{rule.DisplayName}' must be {maximumLength} characters or fewer.");
    }

    /// <summary>
    /// Fails when the string is shorter than <paramref name="minimumLength"/>. A null string passes — pair with <see cref="NotEmpty"/> when it must be there.
    /// </summary>
    public static IRuleBuilder<T, string?> MinimumLength<T>(this IRuleBuilder<T, string?> rule, int minimumLength)
    {
        return rule.AddCheck(
            value => value is null || value.Length >= minimumLength,
            _ => $"The length of '{rule.DisplayName}' must be at least {minimumLength} characters.");
    }

    /// <summary>
    /// Fails when the string does not match <paramref name="pattern"/>. A null string passes.
    /// </summary>
    /// <remarks>
    /// <para>The pattern is compiled once, when the rule is declared, and carries a match timeout so a pathological input cannot hang a request.</para>
    /// </remarks>
    public static IRuleBuilder<T, string?> Matches<T>(this IRuleBuilder<T, string?> rule, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern)
    {
        var expression = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(_REGEX_TIMEOUT_SECONDS));

        return rule.AddCheck(
            value => value is null || expression.IsMatch(value),
            _ => $"'{rule.DisplayName}' is not in the correct format.");
    }

    /// <summary>
    /// Fails when the string is not a plausible email address. A null string passes.
    /// </summary>
    public static IRuleBuilder<T, string?> EmailAddress<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule.AddCheck(IsEmailAddress, _ => $"'{rule.DisplayName}' is not a valid email address.");
    }

    /// <summary>
    /// Fails when the value is not one of the enum's declared members.
    /// </summary>
    /// <remarks>
    /// <para>Catches the case that matters at an API boundary: a number deserialized into an enum-typed property that names no member of that enum.</para>
    /// </remarks>
    public static IRuleBuilder<T, TEnum> IsInEnum<T, TEnum>(this IRuleBuilder<T, TEnum> rule) where TEnum : struct, Enum
    {
        return rule.AddCheck(
            Enum.IsDefined,
            _ => $"'{rule.DisplayName}' has a range of values which does not include the value supplied.");
    }

    /// <summary>
    /// Fails when <paramref name="predicate"/> returns false.
    /// </summary>
    /// <param name="rule">The chain to append to.</param>
    /// <param name="predicate">Returns true when the value is acceptable.</param>
    public static IRuleBuilder<T, TProperty> Must<T, TProperty>(this IRuleBuilder<T, TProperty> rule, Func<TProperty, bool> predicate)
    {
        return rule.AddCheck(predicate, _ => $"The specified condition was not met for '{rule.DisplayName}'.");
    }

    /// <summary>
    /// Runs a check that records its own failures, so one rule can report several distinct problems.
    /// </summary>
    /// <param name="rule">The chain to append to.</param>
    /// <param name="check">Given the value and the run's context, records whatever it finds wrong via <see cref="ValidationContext{T}.AddFailure"/>.</param>
    public static IRuleBuilder<T, TProperty> Custom<T, TProperty>(this IRuleBuilder<T, TProperty> rule, Action<TProperty, ValidationContext<T>> check)
    {
        return rule.AddCustomCheck(check);
    }

    /// <summary>
    /// The emptiness test behind <see cref="NotEmpty"/>: null, blank text and empty sequences are empty, as is the type's own default.
    /// </summary>
    private static bool IsNotEmpty<TProperty>(TProperty value)
    {
        return value switch
        {
            null => false,

            // Ahead of the sequence cases below, since a string is itself a sequence of characters and a blank one should not count as populated.
            string text => string.IsNullOrWhiteSpace(text) == false,

            ICollection { Count: 0 } => false,
            Array { Length: 0 } => false,
            IEnumerable sequence => HasAnyElement(sequence),

            _ => EqualityComparer<TProperty>.Default.Equals(value, default) == false
        };
    }

    /// <summary>
    /// Whether a sequence yields at least one element, disposing the enumerator it walks.
    /// </summary>
    private static bool HasAnyElement(IEnumerable sequence)
    {
        IEnumerator enumerator = sequence.GetEnumerator();

        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// The email test behind <see cref="EmailAddress"/>: exactly one '@', with something on either side of it.
    /// </summary>
    /// <remarks>
    /// <para>The same check ASP.NET Core makes, and the one FluentValidation 12 applies by default. A stricter pattern rejects addresses that are in fact
    /// deliverable, so the only reliable proof that an address works remains sending mail to it.</para>
    /// </remarks>
    private static bool IsEmailAddress(string? value)
    {
        if (value is null)
        {
            return true;
        }

        int atIndex = value.IndexOf('@');

        return atIndex > 0 && atIndex != value.Length - 1 && atIndex == value.LastIndexOf('@');
    }
}
