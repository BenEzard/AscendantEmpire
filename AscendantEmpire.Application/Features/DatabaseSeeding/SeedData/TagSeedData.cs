using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>unitdesigns.Tag</c> catalogue values applied by the database seed.
/// </summary>
/// <remarks>
/// <para>
/// These ride into the database as the <c>Tags</c> list on <see cref="UnitComponentSeedData"/>'s payload rather than as a step of their own, because the component
/// bulk-upsert is the only idempotent path that writes tags: it re-classifies a tag whose code already exists and creates the rest.
/// </para>
/// <para>
/// The organisation tags are what the starting requirements turn on — <c>settler</c> and <c>scout</c> are Unled and so carry their own spawn sizes, and
/// <c>infantry</c> is the Commissioned fallback the human body wears. See <c>docs/design/starting-requirements.md</c>.
/// </para>
/// </remarks>
internal static class TagSeedData
{
    /// <summary>
    /// Builds the tag rows to seed, grouped by tag type.
    /// </summary>
    public static IReadOnlyCollection<TagDto> Build()
    {
        return
        [
            // --- Ability tags -----------------------------------------------------------------
            new TagDto
            {
                Code = CapabilityTagCodes.CREATE_SETTLEMENT,
                DisplayName = "Found Settlement",
                Description = "This can found a settlement.",
                TagType = TagType.Ability,
            },
            new TagDto
            {
                Code = CapabilityTagCodes.CREATE_EARLY_CAMP,
                DisplayName = "Found Camp",
                Description = "Found an early camp.",
                TagType = TagType.Ability,
            },

            // --- Military organisational groups -----------------------------------------------
            // The commissioned fallback: a human body is infantry until a fitted component overrides it.
            new TagDto
            {
                Code = "infantry",
                DisplayName = "Infantry",
                TagType = TagType.MilitaryOrganisationalGroup,
                MovementDomain = DomainType.Land,
                LeadershipModel = LeadershipModel.Commissioned,
            },

            // --- The two starter organisations ------------------------------------------------
            // UnledDefaultSize is the spawn size a new game hands each player: 100 settlers and 1 scout.
            new TagDto
            {
                Code = OrganisationTagCodes.SETTLER,
                DisplayName = "Settler",
                Description = "An unled civilian party.",
                TagType = TagType.MilitaryOrganisationalGroup,
                MovementDomain = DomainType.Land,
                LeadershipModel = LeadershipModel.Unled,
                UnledBaseSize = 25,
                UnledDefaultSize = 100,
                UnledMaxSize = 200,
            },
            new TagDto
            {
                Code = "scout",
                DisplayName = "Scout",
                Description = "A single unled individual, ranging ahead.",
                TagType = TagType.MilitaryOrganisationalGroup,
                MovementDomain = DomainType.Land,
                LeadershipModel = LeadershipModel.Unled,
                UnledBaseSize = 1,
                UnledDefaultSize = 1,
                UnledMaxSize = 1,
            },
        ];
    }
}
