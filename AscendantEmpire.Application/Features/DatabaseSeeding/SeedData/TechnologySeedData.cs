using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>technology.Technology</c> catalogue applied by the database seed.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file is the catalogue.</b> It is authored by hand and is the source of truth - nothing generates it, so an edit goes in here directly and nothing
/// will overwrite it.
/// </para>
/// <para>
/// Matched by code, so re-running the seed renames or re-orders a row rather than duplicating it, and never removes one added by hand. Codes are flat, with no
/// era prefix, and spelled ProperCase with non-alphanumerics stripped. A code is a stable cross-reference key - every prerequisite names one - so renaming a
/// code silently orphans whatever depended on it.
/// </para>
/// <para>
/// SortOrder is left unset throughout and derived from list position, so the order entries appear in here is the order they appear in game. The list is
/// ordered by depth and then by name, which puts the roots first and writes a prerequisite before the technology that needs it; the upsert resolves
/// prerequisites across the whole batch, so that ordering is a courtesy to readers rather than a requirement. Icon paths are left unset until artwork exists.
/// </para>
///
/// <para><b>What is still a placeholder:</b></para>
/// <list type="bullet">
///   <item><description>
///     <b><c>Broken</c>.</b> A placeholder root that 45 technologies hang off because nothing ever established what their real prerequisites are. Without it
///     they would all be Available from the first Round, handing every Player most of the Stone age at once. It is retired by giving each of its dependents
///     real prerequisites and deleting the row. Its <c>DirectedRequired</c> is deliberately out of reach: it is a root, so it is Available from the start, and
///     the catalogue authors no acquisition costs - a cheap root with none would simply be researched, unlocking all 45 at a stroke.
///   </description></item>
///   <item><description>
///     <b>Eras.</b> Everything is <c>TechnologyEra.Stone</c>, which is certainly wrong for the later entries: Copper, Metallurgy, Writing, Banking and
///     Brickworks are not Stone-age technologies. Era only matters to the <c>RandomisedEqual</c> requirement generator, which is not implemented, so nothing
///     reads it yet - but it wants assigning before that lands.
///   </description></item>
///   <item><description>
///     <b>Research costs.</b> Round numbers, not balance decisions, and the one thing in here a reader should not trust.
///   </description></item>
///   <item><description>
///     <b>Experience sources.</b> 27 technologies draw on a seeded <c>ExperienceType</c>, named in <c>ExperienceTypeCodes</c> and matching the "Bonus:"
///     annotation in their description. The rest are directed-only, because nothing connects them to a kind of experience yet.
///   </description></item>
///   <item><description>
///     <b>Grants.</b> Around a hundred entries carry <c>Gives:</c>, <c>Asset:</c>, <c>Unit:</c>, <c>Building:</c>, <c>Specialist:</c> or <c>Action:</c>
///     annotations in their <c>Description</c>. Almost none of them name anything that exists in the Asset, unit-component or template catalogues, so they
///     cannot become <c>TechnologyGrant</c> rows - the upsert would reject the batch for an unresolvable target. <b>The annotation text is kept verbatim</b>
///     so the information is here when those targets exist, and wiring them up means growing <c>TechnologySeedStep.DependsOn</c> to cover whichever
///     catalogues they point at.
///   </description></item>
///   <item><description>
///     <b>Acquisition costs.</b> None. No Asset stockpile is modelled yet - <c>AssetBank</c> and <c>AssetBankBalance</c> are unmapped - so a technology with
///     cost rows could not be unlocked at all.
///   </description></item>
/// </list>
/// </remarks>
internal static class TechnologySeedData
{
    /// <summary>
    /// Builds the whole technology catalogue.
    /// </summary>
    public static IReadOnlyCollection<UpsertTechnologyDto> Build()
    {
        return
        [
            // Nothing in the source material said what these should depend on, so they hang off this instead of being available from turn one. Its
            // requirement is set absurdly high because the catalogue authors no acquisition costs, and a cheap root with none would simply be researched.
            new UpsertTechnologyDto
            {
                Code = "Broken",
                DisplayName = "Broken",
                Description = "Placeholder root standing in for prerequisites the source material never carried. Its dependents await their real ones; emptying them and deleting this row retires it.",
                Era = TechnologyEra.Stone,
                DirectedRequired = 1_000_000
            },
            new UpsertTechnologyDto
            {
                Code = "Amber",
                DisplayName = "Amber",
                Description = "Gives: Hematite",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Banking",
                DisplayName = "Banking",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BasicConstruction",
                DisplayName = "Basic Construction",
                Description = "- Huts",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Brickworks",
                DisplayName = "Brickworks",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CeramicArt",
                DisplayName = "Ceramic Art",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Ceramics",
                DisplayName = "Ceramics",
                Description = "Build: Kiln",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Ceremonies",
                DisplayName = "Ceremonies",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateSkillPolar",
                DisplayName = "Climate Skill - Polar",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CompoundInterest",
                DisplayName = "Compound Interest",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CookingPots",
                DisplayName = "Cooking Pots",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CourtingBehaviour",
                DisplayName = "Courting Behaviour",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CropRotation",
                DisplayName = "Crop Rotation",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyConscription",
                DisplayName = "Early Conscription",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyIndustrialFiring",
                DisplayName = "Early Industrial Firing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ExpertMobileScavengers",
                DisplayName = "Expert Mobile Scavengers",
                Description = "(Extract 10% of tile on the move)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "FireMaking",
                DisplayName = "Fire-Making",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100
            },
            new UpsertTechnologyDto
            {
                Code = "FishPonds",
                DisplayName = "Fish ponds",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "FundamentalsOfMilitaryService",
                DisplayName = "Fundamentals of Military Service",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HorseRiding",
                DisplayName = "Horse Riding",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Horticulture",
                DisplayName = "Horticulture",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Hunting",
                DisplayName = "Hunting",
                Description = "Bonus: Scavenging",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100
            },
            new UpsertTechnologyDto
            {
                Code = "IntensiveGathering",
                DisplayName = "Intensive Gathering",
                Description = "Bonus: Foraging",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Extraction.Foraging"]
            },
            new UpsertTechnologyDto
            {
                Code = "IntroducedSpecies",
                DisplayName = "Introduced Species",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Irrigation",
                DisplayName = "Irrigation",
                Description = "Build: Irrigation Project",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Language",
                DisplayName = "Language",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100
            },
            new UpsertTechnologyDto
            {
                Code = "LargeScaleConstructionProjects",
                DisplayName = "Large Scale Construction Projects",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "LongResearch",
                DisplayName = "Long Research",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Metallurgy",
                DisplayName = "Metallurgy",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Military",
                DisplayName = "Military",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "MobileScavenging",
                DisplayName = "Mobile Scavenging",
                Description = "(Extract 5% of tile on the move)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "NaturalExploration",
                DisplayName = "Natural Exploration",
                Description = "Gives: Scout (from Roving Band unit)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100
            },
            new UpsertTechnologyDto
            {
                Code = "Pastoralist",
                DisplayName = "Pastoralist",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PitFiring",
                DisplayName = "Pit Firing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PoliticalOrganisation",
                DisplayName = "Political Organisation",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PrimitiveCrafting1",
                DisplayName = "Primitive Crafting 1",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100
            },
            new UpsertTechnologyDto
            {
                Code = "PrimitiveSymbolism",
                DisplayName = "Primitive Symbolism",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PropertyOwnership",
                DisplayName = "Property Ownership",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RaidingParties",
                DisplayName = "Raiding Parties",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RegularTrade",
                DisplayName = "Regular Trade",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SailAndMastExpensive",
                DisplayName = "Sail and mast (expensive)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ShortResearch",
                DisplayName = "Short Research",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ShrunkenHeads",
                DisplayName = "Shrunken Heads",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Slings",
                DisplayName = "Slings",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SmithSGuild",
                DisplayName = "Smith's Guild",
                Description = "(on 5th Blacksmith's shop) Building: Blacksmith's Guild Specialists: Apprentice, Journeyman, Master",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SocialOrganisation1",
                DisplayName = "Social Organisation 1",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Tattoos",
                DisplayName = "Tattoos",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Temples",
                DisplayName = "Temples",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainSkillCoastal",
                DisplayName = "Terrain Skill - Coastal",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainSkillOcean",
                DisplayName = "Terrain Skill - Ocean",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TribalWarfare",
                DisplayName = "Tribal Warfare",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Writing",
                DisplayName = "Writing",
                Description = "Gives: Scribes",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Broken" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Acclimitisation",
                DisplayName = "Acclimitisation",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "AdvancedHuntingSkills",
                DisplayName = "Advanced Hunting Skills",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "AnimalDomestication",
                DisplayName = "Animal Domestication",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BandLeadership",
                DisplayName = "Band Leadership",
                Description = "Action: Can split Roving Band unit into two",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Language" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BoneTools",
                DisplayName = "Bone Tools",
                Description = "Gives: Bone Knife Bone resource from Tiles",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PrimitiveCrafting1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateExpertisePolar",
                DisplayName = "Climate Expertise - Polar",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateSkillPolar" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyDiplomacy",
                DisplayName = "Early Diplomacy",
                Description = "Can: Give gifts",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Language" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ForestGardening",
                DisplayName = "Forest Gardening",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "GrassTools",
                DisplayName = "Grass Tools",
                Description = "Uncovers: Bamboo",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PrimitiveCrafting1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HabitatFamiliarisation",
                DisplayName = "Habitat Familiarisation",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HerbLore",
                DisplayName = "Herb Lore",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Ingenuity",
                DisplayName = "Ingenuity",
                Description = "Smart allocation - Experience Research",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PrimitiveCrafting1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "MineralsI",
                DisplayName = "Minerals I",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Ochre",
                DisplayName = "Ochre",
                Description = "Gives: Hematite",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "OralTradition",
                DisplayName = "Oral Tradition",
                Description = "Specialist: Story Teller Define: Empire Bonus",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Language" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Shellfish",
                DisplayName = "Shellfish",
                Description = "(with coastal settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SlashingBurning",
                DisplayName = "Slashing & Burning",
                Description = "Bonus: Foraging",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                ExperienceRequired = 100,
                ExperienceTypeCodes = ["Extraction.Foraging"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SmallGameHunting",
                DisplayName = "Small Game Hunting",
                Description = "Gets: Hunt",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SmokeHouse",
                DisplayName = "Smoke House",
                Description = "Trade Item: Meat & Fish",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "StoneTools",
                DisplayName = "Stone Tools",
                Description = "Gives: Flint",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PrimitiveCrafting1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Swimming",
                DisplayName = "Swimming",
                Description = "(with river/ocean) Bonus: Cross River action",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainMasteryCoastal",
                DisplayName = "Terrain Mastery - Coastal",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainSkillCoastal" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainMasteryOcean",
                DisplayName = "Terrain Mastery - Ocean",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainSkillOcean" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "WoodTools",
                DisplayName = "Wood Tools",
                Description = "Gives: Throwing Spear",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PrimitiveCrafting1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Awls",
                DisplayName = "Awls",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BoneTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BlowPipe",
                DisplayName = "Blow Pipe",
                Description = "Gives: Blow Pipe",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CamelDomestication",
                DisplayName = "Camel Domestication",
                Description = "Gives: Domesticate Camels",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CattleDomestication",
                DisplayName = "Cattle Domestication",
                Description = "Gives: Domesticate Cattle",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Charcoal",
                DisplayName = "Charcoal",
                Description = "Building: Charcoal Pile",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Chopper",
                DisplayName = "Chopper",
                Description = "Bonus: Wood-cutting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Woodcutting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "StoneTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Clay",
                DisplayName = "Clay",
                Description = "Gives: Clay",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateMasteryPolar",
                DisplayName = "Climate Mastery - Polar",
                Description = "(on 5th settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateExpertisePolar" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateSkillModerate",
                DisplayName = "Climate Skill - Moderate",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateSkillSubtropical",
                DisplayName = "Climate Skill - Subtropical",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateSkillTropical",
                DisplayName = "Climate Skill - Tropical",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Copper",
                DisplayName = "Copper",
                Description = "Gives: Copper",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "DiggingStick",
                DisplayName = "Digging Stick",
                Description = "Bonus: Foraging",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Foraging"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Diving",
                DisplayName = "Diving",
                Description = "Asset: Pearl (on coastal)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Swimming" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyBeliefSystems",
                DisplayName = "Early Belief Systems",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "OralTradition" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyConstruction",
                DisplayName = "Early Construction",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyTrade",
                DisplayName = "Early Trade",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyDiplomacy" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Fishing",
                DisplayName = "Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HideScrapers",
                DisplayName = "Hide Scrapers",
                Description = "Gives: Raw Hides",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BoneTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Hoe",
                DisplayName = "Hoe",
                Description = "Bonus: Gathering",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Foraging"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BoneTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Hooks",
                DisplayName = "Hooks",
                Description = "Bonus: Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Fishing"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BoneTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HorseDomestication",
                DisplayName = "Horse Domestication",
                Description = "Action: Domesticate Horses",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HuntingBirds",
                DisplayName = "Hunting Birds",
                Description = "Gives: Hunting Birtds",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Malachite",
                DisplayName = "Malachite",
                Description = "Asset: Malachite",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "MediumGameHunting",
                DisplayName = "Medium Game Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "MineralsII",
                DisplayName = "Minerals II",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "MultiPieceTools",
                DisplayName = "Multi-Piece Tools",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "StoneTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Poisons",
                DisplayName = "Poisons",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HerbLore" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PoultryFarming1",
                DisplayName = "Poultry Farming 1",
                Description = "Asset: Birds Building: Cages",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PreparedCoreTechniques",
                DisplayName = "Prepared Core Techniques",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "StoneTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PrimitiveLeadership",
                DisplayName = "Primitive Leadership",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BandLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PrimitiveMusicalInstruments",
                DisplayName = "Primitive Musical Instruments",
                Description = "Asset: Musical Instrument (Level 1)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "BoneTools",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "WoodTools",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ScentManagement",
                DisplayName = "Scent Management",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AdvancedHuntingSkills" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SelectiveBreeding1",
                DisplayName = "Selective Breeding 1",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Spear",
                DisplayName = "Spear",
                Description = "Gives: Spear",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainSkillHilly",
                DisplayName = "Terrain Skill - Hilly",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HabitatFamiliarisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainSkillIce",
                DisplayName = "Terrain Skill - Ice",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HabitatFamiliarisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainSkillPlain",
                DisplayName = "Terrain Skill - Plain",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HabitatFamiliarisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TrackingLevel1",
                DisplayName = "Tracking (Level 1)",
                Description = "Ability: Tracking",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AdvancedHuntingSkills" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Trapping",
                DisplayName = "Trapping",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AdvancedHuntingSkills" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TribalMedicine",
                DisplayName = "Tribal Medicine",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HerbLore" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "VegetablePlots",
                DisplayName = "Vegetable Plots",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ForestGardening" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Warfare",
                DisplayName = "Warfare",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BandLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Weaving",
                DisplayName = "Weaving",
                Description = "Gives: Baskets Bonus: Foraging rate",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Extraction.Foraging"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "GrassTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "AnimalWorship",
                DisplayName = "Animal worship",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyBeliefSystems" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BambooWhip",
                DisplayName = "Bamboo Whip",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CageTraps",
                DisplayName = "Cage Traps",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CavePaintings",
                DisplayName = "Cave Paintings",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyBeliefSystems" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Ochre" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CharcoalKiln",
                DisplayName = "Charcoal Kiln",
                Description = "Specialist: Collier",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Charcoal" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Clay" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateExpertiseModerate",
                DisplayName = "Climate Expertise - Moderate",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateSkillModerate" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateExpertiseSubtropical",
                DisplayName = "Climate Expertise - Subtropical",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateSkillSubtropical" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateExpertiseTropical",
                DisplayName = "Climate Expertise - Tropical",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateSkillTropical" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ConibearTrap",
                DisplayName = "Conibear Trap",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "DeadFall",
                DisplayName = "Dead-Fall",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "StoneTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "DeathRites",
                DisplayName = "Death Rites",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyBeliefSystems" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "DogDomestication",
                DisplayName = "Dog Domestication",
                Description = "Asset: Dogs Bonus: Settlement Defence",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyPlantDomestication",
                DisplayName = "Early Plant Domestication",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "VegetablePlots" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Engraving",
                DisplayName = "Engraving",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Awls" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "FireHardening",
                DisplayName = "Fire-Hardening",
                Description = "Bonus: Club & Spear",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Spear" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "FishTraps",
                DisplayName = "Fish Traps",
                Description = "Bonus: Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Fishing"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Fishing" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Weaving" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "FootholdTrap",
                DisplayName = "Foothold Trap",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Furs",
                DisplayName = "Furs",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "GoatDomestication",
                DisplayName = "Goat Domestication",
                Description = "Gives: Domesticate Goats",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Gold",
                DisplayName = "Gold",
                Description = "Gives: Gold",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsII" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "GrassClothing",
                DisplayName = "Grass Clothing",
                Description = "Culture Points",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Weaving" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HandAxe",
                DisplayName = "Hand Axe",
                Description = "Gives: Hand Axe Bonus: Foraging",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Foraging"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Chopper" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MultiPieceTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Harpoons",
                DisplayName = "Harpoons",
                Description = "Bonus: Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Fishing"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Fishing" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Spear" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Jewellery",
                DisplayName = "Jewellery",
                Description = "Gives: Jewellery",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Awls" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Knapping",
                DisplayName = "Knapping",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PreparedCoreTechniques" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Lures",
                DisplayName = "Lures",
                Description = "Bonus: Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Fishing"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Fishing" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Mulching",
                DisplayName = "Mulching",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "VegetablePlots" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PigDomestication",
                DisplayName = "Pig Domestication",
                Description = "Gives: Domesticate Pigs",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PunjiSticks",
                DisplayName = "Punji Sticks",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "QuiltedArmour",
                DisplayName = "Quilted Armour",
                Description = "Gives: Quilted Armour",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Awls" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PoultryFarming1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Rafts",
                DisplayName = "Rafts",
                Description = "Action: Cross River",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyConstruction" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RawSinews",
                DisplayName = "Raw Sinews",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PrimitiveCrafting1" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Weaving" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SeasonalTrade",
                DisplayName = "Seasonal Trade",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyTrade" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SheepDomestication",
                DisplayName = "Sheep Domestication",
                Description = "Gives: Domesticate Sheep",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Silver",
                DisplayName = "Silver",
                Description = "Gives: Silver",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsII" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SnakePits",
                DisplayName = "Snake Pits",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SpearThrowers",
                DisplayName = "Spear Throwers",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MultiPieceTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Spear" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainExpertiseHilly",
                DisplayName = "Terrain Expertise - Hilly",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainSkillHilly" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainExpertiseIce",
                DisplayName = "Terrain Expertise - Ice",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainSkillIce" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainExpertisePlain",
                DisplayName = "Terrain Expertise - Plain",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainSkillPlain" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TheMace",
                DisplayName = "The Mace",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TigerTraps",
                DisplayName = "Tiger Traps",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TrappingPit",
                DisplayName = "Trapping Pit",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Adzes",
                DisplayName = "Adzes",
                Description = "Bonus: Farming & Woodcutting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Extraction.Woodcutting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HandAxe" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Knapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BambooStakes",
                DisplayName = "Bamboo Stakes",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "GrassTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Knapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BowArrow",
                DisplayName = "Bow & Arrow",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MultiPieceTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "RawSinews" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BurialMounds",
                DisplayName = "Burial Mounds",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DeathRites" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Cannabilism",
                DisplayName = "Cannabilism",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DeathRites" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateMasteryModerate",
                DisplayName = "Climate Mastery - Moderate",
                Description = "(on 5th settlement; attack bonus)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateExpertiseModerate" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateMasterySubtropical",
                DisplayName = "Climate Mastery - Subtropical",
                Description = "(on 5th settlement; attack bonus)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateExpertiseSubtropical" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateMasteryTropical",
                DisplayName = "Climate Mastery - Tropical",
                Description = "(on 5th settlement; attack bonus)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateExpertiseTropical" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Corn",
                DisplayName = "Corn",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyPlantDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Herding",
                DisplayName = "Herding",
                Description = "+50% domestication birth rate",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "CamelDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "CattleDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "GoatDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "HorseDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "PigDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "SheepDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HuntingDogs",
                DisplayName = "Hunting Dogs",
                Description = "Bonus: Hunting +25% Detection",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DogDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ObservationalMethods",
                DisplayName = "Observational Methods",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SeasonalTrade" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PrimitiveKnife",
                DisplayName = "Primitive Knife",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "HideScrapers",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "Knapping",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ProtectiveClothing",
                DisplayName = "Protective Clothing",
                Description = "Action: Can settle Cold region",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Furs" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RitualDefleshing",
                DisplayName = "Ritual Defleshing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DeathRites" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Rope",
                DisplayName = "Rope",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "RawSinews" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SkinCanoe",
                DisplayName = "Skin Canoe",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Rafts" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Sling",
                DisplayName = "Sling",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "RawSinews",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Snares",
                DisplayName = "Snares",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "RawSinews" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Squash",
                DisplayName = "Squash",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyPlantDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "StoneTipping",
                DisplayName = "Stone-Tipping",
                Description = "Bonus: Spear, Axe, Bow",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Knapping" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MultiPieceTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainMasteryHilly",
                DisplayName = "Terrain Mastery - Hilly",
                Description = "(on 5th settlement; attack bonus)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainExpertiseHilly" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainMasteryIce",
                DisplayName = "Terrain Mastery - Ice",
                Description = "(on 5th settlement; attack bonus)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainExpertiseIce" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TerrainMasteryPlain",
                DisplayName = "Terrain Mastery - Plain",
                Description = "(on 5th settlement; attack bonus)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TerrainExpertisePlain" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Tombs",
                DisplayName = "Tombs",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DeathRites" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Wheat",
                DisplayName = "Wheat",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyPlantDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ArrowNock",
                DisplayName = "Arrow Nock",
                Description = "Bonus: bow damage",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BowArrow" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ArrowWrench",
                DisplayName = "Arrow Wrench",
                Description = "bonus: arrow production",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BowArrow" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BowSilencer",
                DisplayName = "Bow Silencer",
                Description = "bonus: hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                ExperienceRequired = 300,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BowArrow" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateSkillCold",
                DisplayName = "Climate Skill - Cold",
                Description = "(on 1st settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ProtectiveClothing" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "DugoutCanoe",
                DisplayName = "Dugout Canoe",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Adzes" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Rafts" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyFarming",
                DisplayName = "Early Farming",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "Corn",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "Squash",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "Wheat",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Fletching",
                DisplayName = "Fletching",
                Description = "Bonus: bow damage",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BowArrow" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "LargeGameHunting",
                DisplayName = "Large Game Hunting",
                Description = "Unit: Hunting Party",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                ExperienceRequired = 300,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "BowArrow",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "Spear",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Mining1",
                DisplayName = "Mining 1",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Adzes" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Nets",
                DisplayName = "Nets",
                Description = "Bonus Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                ExperienceRequired = 300,
                ExperienceTypeCodes = ["Extraction.Fishing"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Fishing" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Snares" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TrackingLevel2",
                DisplayName = "Tracking (Level 2)",
                Description = "Bonus: Tracking +100%",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "TerrainMasteryHilly",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "TerrainMasteryIce",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "TerrainMasteryPlain",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TrackingLevel1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClayPit",
                DisplayName = "Clay Pit",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Clay" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Mining1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateExpertiseCold",
                DisplayName = "Climate Expertise - Cold",
                Description = "(on 3rd settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateSkillCold" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "LeatherTanning",
                DisplayName = "Leather Tanning",
                Description = "Building: Drying Racks",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LargeGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "OutriggerCanoe",
                DisplayName = "Outrigger Canoe",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DugoutCanoe" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PersistenceHunting",
                DisplayName = "Persistence Hunting",
                Description = "Hunting Bonus",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                ExperienceRequired = 350,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LargeGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PlowAnimals",
                DisplayName = "Plow Animals",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "CattleDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyFarming" },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "HorseDomestication",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ClimateMasteryCold",
                DisplayName = "Climate Mastery - Cold",
                Description = "(on 5th settlement)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 800,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ClimateExpertiseCold" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "LeatherArmour",
                DisplayName = "Leather Armour",
                Era = TechnologyEra.Stone,
                DirectedRequired = 800,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LeatherTanning" }
                ]
            }
        ];
    }
}
