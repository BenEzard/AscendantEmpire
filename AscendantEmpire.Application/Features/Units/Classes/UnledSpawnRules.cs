using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.Units.Classes;

/// <summary>
/// The unled sizing rules applied when a troop is created: an unled unit's size must be a whole multiple of
/// its organisation tag's base quantum, no larger than the tag's max.
/// </summary>
/// <remarks>
/// <para>
/// Shared by the dev spawn endpoint and game-start spawning so the rule cannot drift between the two paths.
/// Commissioned (and untagged) templates pass through untouched — their tier-size rules are a separate,
/// still-outstanding concern (see <c>docs/design/units/military-organisation-implementation-notes.md</c>).
/// </para>
/// </remarks>
internal static class UnledSpawnRules
{
    /// <summary>
    /// Returns every problem with creating a troop of <paramref name="size"/> under
    /// <paramref name="organisationTag"/>, or an empty list when the size is legal (or no unled rule applies).
    /// </summary>
    public static List<string> ValidateSize(Tag? organisationTag, int size)
    {
        List<string> errors = [];

        // No organisation tag, or a commissioned one, imposes no unled sizing rule.
        if (organisationTag is not { LeadershipModel: LeadershipModel.Unled })
        {
            return errors;
        }

        // An unled tag is authored with all three sizes (enforced at tag-save time); treat missing data as
        // a configuration error rather than silently skipping the guardrail.
        if (organisationTag.UnledBaseSize is not > 0 || organisationTag.UnledMaxSize is not > 0)
        {
            errors.Add($"Organisation tag '{organisationTag.Code}' is Unled but has no authored base/max sizes.");
            return errors;
        }

        int baseSize = organisationTag.UnledBaseSize.Value;
        int maxSize = organisationTag.UnledMaxSize.Value;

        // The floor is one whole quantum. Checked separately from the multiple rule below because zero (and
        // any negative multiple) divides cleanly by the base and would otherwise slip through.
        if (size < baseSize)
        {
            errors.Add($"An unled '{organisationTag.Code}' unit must be at least {baseSize} strong (got {size}).");
        }
        else if (size % baseSize != 0)
        {
            errors.Add($"An unled '{organisationTag.Code}' unit must be a whole multiple of {baseSize} (got {size}).");
        }

        if (size > maxSize)
        {
            errors.Add($"An unled '{organisationTag.Code}' unit may be at most {maxSize} strong (got {size}).");
        }

        return errors;
    }
}
