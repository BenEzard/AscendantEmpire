using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Classes;

/// <summary>
/// Shared shape validation for a single tag type upsert, used by the bulk upsert handler.
/// </summary>
/// <remarks>
/// Mirrors <see cref="TagValidation"/>: a required, length-bounded <c>DisplayName</c> and
/// <c>Description</c>, and a defined <c>TagTypeId</c> enum value.
/// </remarks>
internal static class TagTypeValidation
{
    /// <summary>
    /// Returns every shape problem with the tag type, or an empty list when it is valid.
    /// </summary>
    public static List<string> Validate(UpsertTagTypeDto tagType)
    {
        List<string> errors = [];

        if (!Enum.IsDefined(tagType.TagTypeId))
        {
            errors.Add("The tag type has an undefined TagTypeId value.");
        }

        if (string.IsNullOrWhiteSpace(tagType.DisplayName))
        {
            errors.Add("The tag type is missing its DisplayName.");
        }
        else if (tagType.DisplayName.Length > TagTypeInfo.DISPLAY_NAME_MAX_LENGTH)
        {
            errors.Add($"Tag type DisplayName exceeds {TagTypeInfo.DISPLAY_NAME_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(tagType.Description))
        {
            errors.Add("The tag type is missing its Description.");
        }
        else if (tagType.Description.Length > TagTypeInfo.DESCRIPTION_MAX_LENGTH)
        {
            errors.Add($"Tag type Description exceeds {TagTypeInfo.DESCRIPTION_MAX_LENGTH} characters.");
        }

        if (tagType.Colour is { Length: > TagTypeInfo.COLOUR_MAX_LENGTH })
        {
            errors.Add($"Tag type Colour exceeds {TagTypeInfo.COLOUR_MAX_LENGTH} characters.");
        }

        if (tagType.Icon is { Length: > TagTypeInfo.ICON_MAX_LENGTH })
        {
            errors.Add($"Tag type Icon exceeds {TagTypeInfo.ICON_MAX_LENGTH} characters.");
        }

        return errors;
    }
}
