using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Creates or re-classifies <c>unitdesigns.Tag</c> rows from a payload's tag definitions, matched by <see cref="Tag.Code"/>.
/// </summary>
/// <remarks>
/// <para>
/// Shared by the two idempotent paths that write tags in bulk: the component catalogue upsert, where the tags travel alongside the components that
/// reference them, and the tags-only upsert the database seed uses to refresh the tag catalogue on its own. Both must agree on what "the payload wins"
/// means, which is why the loop lives here rather than in either handler.
/// </para>
/// <para>
/// The payload is authoritative on re-import: an existing tag keeps its identity and its component joins, but its classification and organisation fields
/// are overwritten to match. That is what makes a re-run of the seed converge rather than accumulate.
/// </para>
/// <para>
/// Nothing is saved here — the caller owns the <c>SaveChangesAsync</c>, so a tag upsert can share one transaction with the components written alongside it.
/// </para>
/// </remarks>
internal static class TagCatalogueUpserter
{
    /// <summary>
    /// Applies <paramref name="tags"/> to the context, updating the rows already present in <paramref name="tagsByCode"/> and adding the rest.
    /// </summary>
    /// <param name="context">The context the new tags are added to. Not saved.</param>
    /// <param name="tags">The tag definitions to apply. An empty collection is a no-op.</param>
    /// <param name="tagsByCode">
    /// Tags already loaded from the database, keyed by code — typically from <see cref="UnitComponentReconciler.LoadTagsByCodeAsync"/>. Newly created tags
    /// are added to it, so a caller that resolves component tag references against this dictionary sees the tags created in the same pass.
    /// </param>
    /// <returns>The number of tags added, so a caller can report what it did.</returns>
    public static int Upsert(AscendantEmpireContext context, IReadOnlyCollection<TagDto> tags, Dictionary<string, Tag> tagsByCode)
    {
        int addedCount = 0;

        foreach (TagDto tagDto in tags)
        {
            if (tagsByCode.TryGetValue(tagDto.Code, out Tag? existingTag))
            {
                existingTag.TagType = tagDto.TagType;
                existingTag.MovementDomain = tagDto.MovementDomain;
                existingTag.LeadershipModel = tagDto.LeadershipModel;
                existingTag.UnledBaseSize = tagDto.UnledBaseSize;
                existingTag.UnledDefaultSize = tagDto.UnledDefaultSize;
                existingTag.UnledMaxSize = tagDto.UnledMaxSize;
            }
            else
            {
                var tag = new Tag
                {
                    Code = tagDto.Code,
                    DisplayName = tagDto.DisplayName,
                    Description = tagDto.Description,
                    TagType = tagDto.TagType,
                    MovementDomain = tagDto.MovementDomain,
                    LeadershipModel = tagDto.LeadershipModel,
                    UnledBaseSize = tagDto.UnledBaseSize,
                    UnledDefaultSize = tagDto.UnledDefaultSize,
                    UnledMaxSize = tagDto.UnledMaxSize,
                };

                context.Tags.Add(tag);
                tagsByCode[tagDto.Code] = tag;
                addedCount++;
            }
        }

        return addedCount;
    }

    /// <summary>
    /// Validates one payload tag's own fields — code, display name, tag type and the organisation rules.
    /// </summary>
    /// <remarks>
    /// The organisation rules are shared with the single-tag create/update path, so a bulk payload cannot slip past the checks the Tag manager enforces.
    /// </remarks>
    /// <param name="tag">The tag definition to check.</param>
    /// <param name="errors">Collects every problem found, so the caller can report them all at once rather than one at a time.</param>
    public static void Validate(TagDto tag, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(tag.Code))
        {
            errors.Add("A tag is missing its Code.");
        }
        else if (tag.Code.Length > Tag.CODE_MAX_LENGTH)
        {
            errors.Add($"Tag code '{tag.Code}' exceeds {Tag.CODE_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(tag.DisplayName))
        {
            errors.Add($"Tag '{tag.Code}' is missing its DisplayName.");
        }
        else if (tag.DisplayName.Length > Tag.DISPLAY_NAME_MAX_LENGTH)
        {
            errors.Add($"Tag '{tag.Code}' DisplayName exceeds {Tag.DISPLAY_NAME_MAX_LENGTH} characters.");
        }

        if (Enum.IsDefined(tag.TagType) == false)
        {
            errors.Add($"Tag '{tag.Code}' has an undefined TagType value.");
        }

        errors.AddRange(TagValidation.ValidateOrganisationFields(
            tag.TagType,
            tag.MovementDomain,
            tag.LeadershipModel,
            tag.UnledBaseSize,
            tag.UnledDefaultSize,
            tag.UnledMaxSize,
            tag.Code));
    }

    /// <summary>
    /// Finds tag codes that appear more than once in a payload, which would otherwise upsert the same row twice with different values.
    /// </summary>
    /// <param name="tags">The tag definitions to check.</param>
    /// <returns>The duplicated codes, or an empty array when every code is distinct.</returns>
    public static string[] FindDuplicateCodes(IReadOnlyCollection<TagDto> tags)
    {
        return [.. tags
            .GroupBy(tag => tag.Code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
    }
}
