using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Classes;

/// <summary>
/// Shared shape validation for a single tag upsert, used by both the create and update handlers.
/// </summary>
/// <remarks>
/// Mirrors the per-tag checks the bulk-upsert handler applies: a required, length-bounded
/// <c>Code</c> and <c>DisplayName</c>, and a defined <c>TagType</c>. Uniqueness of the code is a
/// database concern and is checked by each handler against the current rows, not here.
/// </remarks>
internal static class TagValidation
{
    /// <summary>
    /// Returns every shape problem with the tag, or an empty list when it is valid.
    /// </summary>
    public static List<string> Validate(UpsertTagDto tag)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(tag.Code))
        {
            errors.Add("The tag is missing its Code.");
        }
        else if (tag.Code.Length > Tag.CODE_MAX_LENGTH)
        {
            errors.Add($"Tag code '{tag.Code}' exceeds {Tag.CODE_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(tag.DisplayName))
        {
            errors.Add("The tag is missing its DisplayName.");
        }
        else if (tag.DisplayName.Length > Tag.DISPLAY_NAME_MAX_LENGTH)
        {
            errors.Add($"Tag DisplayName exceeds {Tag.DISPLAY_NAME_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(tag.Description))
        {
            errors.Add("The tag is missing its Description.");
        }
        else if (tag.Description.Length > Tag.DESCRIPTION_MAX_LENGTH)
        {
            errors.Add($"Tag Description exceeds {Tag.DESCRIPTION_MAX_LENGTH} characters.");
        }


        if (!Enum.IsDefined(tag.TagType))
        {
            errors.Add("The tag has an undefined TagType value.");
        }

        return errors;
    }
}
