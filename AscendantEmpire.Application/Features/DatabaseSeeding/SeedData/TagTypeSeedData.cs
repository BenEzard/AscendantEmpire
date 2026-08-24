using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>unitdesigns.TagType</c> catalogue values applied by the database seed.
/// </summary>
/// <remarks>
/// <para>
/// This is the single source of truth for the tag type catalogue — the values that used to live in the
/// <c>TagTypeInfo</c> EF <c>HasData</c> block now live here so they can be edited without a migration.
/// </para>
/// <para>
/// The set is fixed by the <see cref="TagType"/> enum; add a member here whenever a new enum value is
/// introduced. Colour and Icon start unset and are filled in later through the Tag Type manager.
/// </para>
/// </remarks>
internal static class TagTypeSeedData
{
    /// <summary>
    /// Builds the tag type rows to seed, one per <see cref="TagType"/> enum member.
    /// </summary>
    public static IReadOnlyCollection<UpsertTagTypeDto> Build()
    {
        return
        [
            new UpsertTagTypeDto
            {
                TagTypeId = TagType.Classification,
                DisplayName = "Classification",
                Description = "A structural classification of a component, such as its family or role.",
                IsInheritedToBase = false,
                Colour = null,
                Icon = null,
            },
            new UpsertTagTypeDto
            {
                TagTypeId = TagType.Combat,
                DisplayName = "Combat",
                Description = "A temporary or permanent enhancement or buff to combat.",
                IsInheritedToBase = false,
                Colour = null,
                Icon = null,
            },
            new UpsertTagTypeDto
            {
                TagTypeId = TagType.Movement,
                DisplayName = "Movement",
                Description = "A temporary or permanent enhancement or buff to movement.",
                IsInheritedToBase = false,
                Colour = null,
                Icon = null,
            },
            new UpsertTagTypeDto
            {
                TagTypeId = TagType.Ability,
                DisplayName = "Ability",
                Description = "A unique capability or function of a unit component.",
                IsInheritedToBase = false,
                Colour = null,
                Icon = null,
            },
            new UpsertTagTypeDto
            {
                TagTypeId = TagType.MilitaryOrganisationalGroup,
                DisplayName = "Military Organisational Group",
                Description = "The organisational group this fits into when organising the military into hierarchy.",
                // The only inheriting tag type today. A fitted component carrying an organisation tag overrides the
                // body's default one, so a human body is Infantry (Commissioned) until camping goods fitted alongside
                // it make the design a Settler (Unled).
                IsInheritedToBase = true,
                Colour = null,
                Icon = null,
            },
        ];
    }
}
