using System.Linq.Expressions;
using System.Text;

namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// Turns a property selector into the display name that default messages are built from.
/// </summary>
internal static class PropertyNameFormatter
{
    /// <summary>
    /// Resolves the display name for the property a selector reads.
    /// </summary>
    /// <remarks>
    /// <para>A selector that reads no property at all — <c>x =&gt; x</c>, used for rules over the whole instance — falls back to the validated type's own
    /// name. Every such rule in the codebase supplies its own message, so the fallback only ever surfaces for a rule written without one.</para>
    /// </remarks>
    /// <param name="selector">The lambda passed to <c>RuleFor</c> or <c>RuleForEach</c>.</param>
    /// <param name="validatedType">The type being validated, used when the selector names no member.</param>
    /// <returns>The member name split into words, for example <c>Map Name</c>.</returns>
    public static string DisplayNameFor(LambdaExpression selector, Type validatedType)
    {
        return SplitPascalCase(MemberNameOf(selector.Body) ?? validatedType.Name);
    }

    /// <summary>
    /// Reads the member name out of a selector body, or null when the selector names no member.
    /// </summary>
    private static string? MemberNameOf(Expression body)
    {
        // A selector whose property type does not match the lambda's declared return type arrives wrapped in a conversion, so unwrap before looking for the
        // member itself.
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
        {
            body = conversion.Operand;
        }

        return body switch
        {
            MemberExpression member => member.Member.Name,
            _ => null
        };
    }

    /// <summary>
    /// Inserts spaces at word boundaries in a PascalCase name, so <c>MapName</c> reads as <c>Map Name</c>.
    /// </summary>
    /// <remarks>
    /// <para>Reproduces the algorithm FluentValidation used to build its default messages. One rule in the codebase relies on a generated message — the
    /// bare <c>NotEmpty()</c> on <c>MapName</c> in the campaign map index validators — and this keeps it reading exactly as it did.</para>
    /// </remarks>
    /// <param name="input">The member name to split.</param>
    public static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder(input.Length + 5);

        for (int index = 0; index < input.Length; index++)
        {
            char current = input[index];

            // A capital opens a new word when it follows a lower-case letter, or when it is the last capital of a run — so "MapName" splits but an acronym
            // like "ID" stays whole.
            bool opensWord = char.IsUpper(current)
                && ((index > 1 && char.IsUpper(input[index - 1]) == false)
                    || (index + 1 < input.Length && char.IsUpper(input[index + 1]) == false));

            if (opensWord)
            {
                result.Append(' ');
            }

            result.Append(current);
        }

        return result.ToString().Trim();
    }
}
