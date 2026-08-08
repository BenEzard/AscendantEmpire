using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

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

        // Description is optional (the column is nullable), so only bound its length when one is supplied.
        if (!string.IsNullOrWhiteSpace(tag.Description) && tag.Description.Length > Tag.DESCRIPTION_MAX_LENGTH)
        {
            errors.Add($"Tag Description exceeds {Tag.DESCRIPTION_MAX_LENGTH} characters.");
        }


        if (!Enum.IsDefined(tag.TagType))
        {
            errors.Add("The tag has an undefined TagType value.");
        }

        errors.AddRange(ValidateOrganisationFields(
            tag.TagType,
            tag.MovementDomain,
            tag.LeadershipModel,
            tag.UnledBaseSize,
            tag.UnledDefaultSize,
            tag.UnledMaxSize));

        return errors;
    }

    /// <summary>
    /// Returns every problem with a tag's military-organisation fields — the movement domain, the leadership
    /// model, and the unled sizing the model gates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by the single-tag create/update path and the catalogue bulk-upsert path, so importing a
    /// catalogue cannot become a back door around the rules the Tag manager enforces.
    /// </para>
    /// <para>
    /// <paramref name="tagCode"/> qualifies the messages when the caller validates many tags at once; leave it
    /// null when the request is already about one known tag.
    /// </para>
    /// </remarks>
    public static List<string> ValidateOrganisationFields(
        TagType tagType,
        DomainType? movementDomain,
        LeadershipModel? leadershipModel,
        int? unledBaseSize,
        int? unledDefaultSize,
        int? unledMaxSize,
        string? tagCode = null)
    {
        List<string> errors = [];

        string prefix = string.IsNullOrWhiteSpace(tagCode) ? string.Empty : $"Tag '{tagCode}': ";

        // A movement domain is only meaningful for organisation-group tags (it ties an organisation hierarchy
        // to a domain). Reject it on any other type, and reject an undefined enum value.
        if (movementDomain.HasValue)
        {
            if (tagType != TagType.MilitaryOrganisationalGroup)
            {
                errors.Add($"{prefix}A movement domain can only be set on a Military Organisational Group tag.");
            }
            else if (!Enum.IsDefined(movementDomain.Value))
            {
                errors.Add($"{prefix}The tag has an undefined MovementDomain value.");
            }
        }

        // Like the movement domain, the leadership model (and the unled sizing it gates) is only meaningful
        // for organisation-group tags.
        if (leadershipModel.HasValue)
        {
            if (tagType != TagType.MilitaryOrganisationalGroup)
            {
                errors.Add($"{prefix}A leadership model can only be set on a Military Organisational Group tag.");
            }
            else if (!Enum.IsDefined(leadershipModel.Value))
            {
                errors.Add($"{prefix}The tag has an undefined LeadershipModel value.");
            }
        }

        if (leadershipModel == LeadershipModel.Unled)
        {
            // An unled tag's sizing is authored here on the tag (there is no per-player structure and no rank
            // ceiling to bound it), so all three values are required and must be coherent.
            if (unledBaseSize is not > 0)
            {
                errors.Add($"{prefix}An Unled tag requires an UnledBaseSize greater than 0.");
            }

            if (unledDefaultSize is not > 0)
            {
                errors.Add($"{prefix}An Unled tag requires an UnledDefaultSize greater than 0.");
            }

            if (unledMaxSize is not > 0)
            {
                errors.Add($"{prefix}An Unled tag requires an UnledMaxSize greater than 0.");
            }

            // Only check the cross-field rules once the individual values are present and positive.
            if (unledBaseSize is > 0 && unledDefaultSize is > 0 && unledMaxSize is > 0)
            {
                if (unledBaseSize > unledDefaultSize || unledDefaultSize > unledMaxSize)
                {
                    errors.Add($"{prefix}Unled sizes must satisfy UnledBaseSize <= UnledDefaultSize <= UnledMaxSize.");
                }

                // Every legal unled size is a whole multiple of the base quantum — including the default.
                if (unledDefaultSize % unledBaseSize != 0)
                {
                    errors.Add($"{prefix}UnledDefaultSize must be a whole multiple of UnledBaseSize.");
                }
            }
        }
        else if (unledBaseSize.HasValue || unledDefaultSize.HasValue || unledMaxSize.HasValue)
        {
            errors.Add($"{prefix}Unled sizes can only be set on a tag whose LeadershipModel is Unled.");
        }

        return errors;
    }
}
