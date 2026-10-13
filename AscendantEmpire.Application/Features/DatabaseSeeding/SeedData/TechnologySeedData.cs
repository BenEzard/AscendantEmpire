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
/// ordered by depth and then by code, which puts the roots first and normally writes a prerequisite before the technology that needs it; the upsert resolves
/// prerequisites across the whole batch, so that ordering is a courtesy to readers rather than a requirement, and a repair that leaves a technology at its
/// original cost may legitimately point it at a prerequisite further down. Icon paths are left unset until artwork exists.
/// </para>
///
/// <para><b>What is still a placeholder:</b></para>
/// <list type="bullet">
///   <item><description>
///     <b>Eras.</b> Everything is <c>TechnologyEra.Stone</c>, which is certainly wrong for the later entries: Copper, Metallurgy, Writing, Banking and
///     Brickworks are not Stone-age technologies. Era only matters to the <c>RandomisedEqual</c> requirement generator, which is not implemented, so nothing
///     reads it yet - but it wants assigning before that lands.
///   </description></item>
///   <item><description>
///     <b>Research costs.</b> Round numbers, not balance decisions, and the one thing in here a reader should not trust.
///   </description></item>
///   <item><description>
///     <b>Experience sources.</b> 61 technologies draw on a seeded <c>ExperienceType</c>, named in <c>ExperienceTypeCodes</c> and matching the "Bonus:"
///     annotation in their description. The rest are directed-only, because nothing connects them to a kind of experience yet.
///   </description></item>
///   <item><description>
///     <b>Grants.</b> Fifty entries carry <c>Gives:</c>, <c>Asset:</c>, <c>Unit:</c>, <c>Building:</c>, <c>Specialist:</c> or <c>Action:</c>
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
            new UpsertTechnologyDto
            {
                Code = "Banking",
                DisplayName = "Banking",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyCurrency" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BasicCeramics" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Metallurgy" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "OralTradition" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Horticulture" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FundamentalsOfMilitaryService" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MobileScavenging" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AdvancedHuntingSkills" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "FireMaking",
                DisplayName = "Fire-Making",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Exploration"]
            },
            new UpsertTechnologyDto
            {
                Code = "FishPonds",
                DisplayName = "Fish Ponds",
                Description = "Building: Fish Pond on shore/river",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumScaleConstructionProjects" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FishTraps" },
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalWarfare" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HorseDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HorseShoe",
                DisplayName = "Horse Shoe",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HorseRiding" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CalvaryTactics",
                DisplayName = "Calvary Tactics",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HorseRiding" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "VegetablePlots" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Hunting",
                DisplayName = "Hunting",
                Description = "Bonus: Scavenging",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Exploration"]
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CageTraps", PrerequisiteKind = TechnologyPrerequisiteKind.Optional },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ConibearTrap", PrerequisiteKind = TechnologyPrerequisiteKind.Optional },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FootholdTrap", PrerequisiteKind = TechnologyPrerequisiteKind.Optional },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TrappingPit", PrerequisiteKind = TechnologyPrerequisiteKind.Optional },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Snares", PrerequisiteKind = TechnologyPrerequisiteKind.Optional },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Language",
                DisplayName = "Language",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Exploration"]
            },
            new UpsertTechnologyDto
            {
                Code = "MediumScaleConstructionProjects",
                DisplayName = "Medium Scale Construction Projects",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalProjects" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "LargeScaleConstructionProjects",
                DisplayName = "Large Scale Construction Projects",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumScaleConstructionProjects" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Terraces",
                DisplayName = "Terraces",
                Description = "Increased farming on hills",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LargeScaleConstructionProjects" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Horticulture" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Causeways",
                DisplayName = "Causeways",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LargeScaleConstructionProjects" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Horticulture" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Metallurgy",
                DisplayName = "Metallurgy",
                Description = "Building: Blacksmith",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CharcoalKiln" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Blacksmith",
                DisplayName = "Blacksmith",
                Description = "Specialist: Apprentice, Journeyman, Blacksmith",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Metallurgy" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Swordsmith",
                DisplayName = "Swordsmith",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Blacksmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Fuller",
                DisplayName = "Fuller",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Swordsmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Indexing",
                DisplayName = "Indexing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Swordsmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Pommel",
                DisplayName = "Pommel",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Swordsmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Guard",
                DisplayName = "Guard",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Swordsmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CeratedEdges",
                DisplayName = "Cerated Edges",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Swordsmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SmithsWagon",
                DisplayName = "Smith's Wagon",
                Description = "Mobile smith shop",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Blacksmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Locksmith",
                DisplayName = "Locksmith",
                Description = "Specialist: Locksmith",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Blacksmith" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Arnmourer",
                DisplayName = "Armourer",
                Description = "Specialist: Armourer",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Blacksmith" },
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HorseRiding" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "NaturalExploration",
                DisplayName = "Natural Exploration",
                Description = "Gives: Scout (from Roving Band unit)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Exploration"]
            },
            new UpsertTechnologyDto
            {
                Code = "Pastoralist",
                DisplayName = "Pastoralist",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalTaming" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PrimitiveCrafting1",
                DisplayName = "Primitive Crafting 1",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Exploration"]
            },
            new UpsertTechnologyDto
            {
                Code = "PrimitiveSymbolism",
                DisplayName = "Primitive Symbolism",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyBeliefSystems" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CityLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Numbers",
                DisplayName = "Numbers",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Language" }
                ]
            },

            new UpsertTechnologyDto
            {
                Code = "EarlyCurrency",
                DisplayName = "Early Currency",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Numbers" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "OutriggerCanoe" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DeathRites" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SkullRacks",
                DisplayName = "Skull Racs",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DeathRites" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SmithsGuild",
                DisplayName = "Smith's Guild",
                Description = "(on 5th Blacksmith's shop) Building: Blacksmith's Guild, Specialists: Master Blacksmith",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Metallurgy" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Guilds" }
                ]
            },

            new UpsertTechnologyDto
            {
                Code = "Guilds",
                DisplayName = "Guild",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CityLeadership" },
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Ceremonies" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Temples",
                DisplayName = "Temples",
                Description = "Gives Priests",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Shrines" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Altars",
                DisplayName = "Altars",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Shrines" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Shrines",
                DisplayName = "Shrines",
                Era = TechnologyEra.Stone,
                DirectedRequired = 100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyBeliefSystems" },
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "OralTradition" }
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
                Code = "BandLeadership",
                DisplayName = "Band Leadership",
                Description = "Action: Can split Roving Band unit down to the minimum band size",
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
                Code = "BasicCeramics",
                DisplayName = "Basic Ceramics",
                Description = "Build: Kiln",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CommonCharcoal",
                DisplayName = "Common Charcoal",
                Description = "Building: Charcoal Pile",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Briquettes",
                DisplayName = "Briquettes",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CommonCharcoal" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ActivatedCharcoal",
                DisplayName = "Activated Charcoal",
                Description = "for medical purposes",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CommonCharcoal" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CompoundInterest",
                DisplayName = "Compound Interest",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Banking" }
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
                Code = "EarlyIndustrialFiring",
                DisplayName = "Early Industrial Firing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyPlantDomestication",
                DisplayName = "Early Plant Domestication",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
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
                Code = "Jewellery",
                DisplayName = "Jewellery",
                Description = "Gives: Jewellery",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
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
                Code = "Mining1",
                DisplayName = "Mining 1",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
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
                Code = "PitFiring",
                DisplayName = "Pit Firing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" }
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
                Code = "OceanMastery",
                DisplayName = "Ocean Mastery",
                Era = TechnologyEra.Stone,
                DirectedRequired = 200,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "OceanNative" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "WoodTools",
                DisplayName = "Wood Tools",
                Description = "Gives: Throwing Spear Resource: Wood from Tiles",
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
                Code = "BasicShelterConstruction",
                DisplayName = "Basic Shelter Construction",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "GrassTools",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Language" },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "WoodTools",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillArid",
                DisplayName = "Biome Skill - Arid",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Biome.Arid"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillForest",
                DisplayName = "Biome Skill - Forest",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Biome.Forest"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillGrassland",
                DisplayName = "Biome Skill - Grassland",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Biome.Grassland"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillJungle",
                DisplayName = "Biome Skill - Jungle",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Biome.Jungle"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillLake",
                DisplayName = "Biome Skill - Lake",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillMangrove",
                DisplayName = "Biome Skill - Mangrove",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillWoodland",
                DisplayName = "Biome Skill - Woodland",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                ExperienceRequired = 150,
                ExperienceTypeCodes = ["Biome.Woodland"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Acclimitisation" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BoneTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BogOre",
                DisplayName = "Bog Ore",
                Description = "Gives: Bog Ore",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Brickworks",
                DisplayName = "Brickworks",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyIndustrialFiring" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Ceremonies",
                DisplayName = "Ceremonies",
                Description = "Gives feathered headdress (although should also need birds???)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Shrines" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlySpiritualBeliefs",
                DisplayName = "Early Spiritual Beliefs",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Language" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Animinism",
                DisplayName = "Animinism",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlySpiritualBeliefs" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "AnimalWorship",
                DisplayName = "Animal Worship",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlySpiritualBeliefs" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Totenism",
                DisplayName = "Totenism",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalWorship" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CharcoalKiln",
                DisplayName = "Charcoal Kiln",
                Description = "Specialist: Collier Building: Charcoal Kiln (60% charcoal by volume up to 90%)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CommonCharcoal" }
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
                Code = "Corn",
                DisplayName = "Corn",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyPlantDomestication" }
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
                Description = "Gives witchdoctor",
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
                Code = "Gold",
                DisplayName = "Gold",
                Description = "Gives: Gold",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HabitatModification",
                DisplayName = "Habitat Modification",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HabitatFamiliarisation" }
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
                Code = "Lead",
                DisplayName = "Lead",
                Description = "Asset: Lead Distribution: Lead (from Stone and Wood) Trade Item: Lead",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
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
                Description = "Bonus: Hunting Asset, Trade Good: Poison Battle Action: Poison Weapons",
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
                Code = "PrimitiveMusicalInstruments",
                DisplayName = "Primitive Musical Instruments",
                Description = "Asset: Musical Instrument (Level 1)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BoneTools" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
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
                Code = "Silver",
                DisplayName = "Silver",
                Description = "Gives: Silver",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
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
                Code = "Squash",
                DisplayName = "Squash",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyPlantDomestication" }
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
                Code = "OceanFamiliarity",
                DisplayName = "Ocean Familiarity",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HabitatFamiliarisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "OceanNative",
                DisplayName = "Ocean Native",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "OceanFamiliarity" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CoastalMastery" }
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
                Code = "Tin",
                DisplayName = "Tin",
                Description = "Asset: Tin Distribution: Tin (from Stone and Wood) Trade Item: Tin",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MineralsI" }
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
                Description = "Bonus: Hunting Specialist: Trapper",
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
                Code = "TribalLeadership",
                DisplayName = "Tribal Leadership",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BandLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CityLeadership",
                DisplayName = "City Leadership",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RegionalLeadership",
                DisplayName = "Regional Leadership",
                Description = "On 5th city",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CityLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EmpireLeadership",
                DisplayName = "Empire Leadership",
                Description = "On 10th city",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "RegionalLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TribalMedicine",
                DisplayName = "Tribal Medicine",
                Description = "Asset, Trade Good: Medicine (passive creation)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HerbLore" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TribalWarfare",
                DisplayName = "Tribal Warfare",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BandLeadership" }
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
                Code = "Wheat",
                DisplayName = "Wheat",
                Era = TechnologyEra.Stone,
                DirectedRequired = 300,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyPlantDomestication" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "AnimalTaming",
                DisplayName = "Animal Taming",
                Description = "Bonus: Settlement Defence Food: Dog Building: Animal Enclosure",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "NaturalExploration" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
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
                Code = "Antidotes",
                DisplayName = "Antidotes",
                Description = "Specialist: Healer (bonus passive Tribal Medicine production)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Poisons" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalMedicine" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeExpertiseArid",
                DisplayName = "Biome Expertise - Arid",
                Description = "(on 3rd settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Biome.Arid"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillArid" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeExpertiseForest",
                DisplayName = "Biome Expertise - Forest",
                Description = "(on 3rd settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Biome.Forest"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillForest" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeExpertiseGrassland",
                DisplayName = "Biome Expertise - Grassland",
                Description = "(on 3rd settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Biome.Grassland"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillGrassland" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeExpertiseJungle",
                DisplayName = "Biome Expertise - Jungle",
                Description = "(on 3rd settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Biome.Jungle"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillJungle" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeExpertiseLake",
                DisplayName = "Biome Expertise - Lake",
                Description = "(on 3rd settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillLake" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeExpertiseMangrove",
                DisplayName = "Biome Expertise - Mangrove",
                Description = "(on 3rd settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillMangrove" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeExpertiseWoodland",
                DisplayName = "Biome Expertise - Woodland",
                Description = "(on 3rd settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Biome.Woodland"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillWoodland" }
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
                Code = "ClayPit",
                DisplayName = "Clay Pit",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Clay" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Mining1" }
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
                Code = "Rituals",
                DisplayName = "Rituals",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Ceremonies" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RitualSacrifice",
                DisplayName = "Ritual Sacrifice",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Rituals" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RitualVessels",
                DisplayName = "Ritual Vessels",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Rituals" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "EarlyFarming",
                DisplayName = "Early Farming",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
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
                Code = "ForestFamiliarity",
                DisplayName = "Forest Familiarity",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "ClimateSkillModerate",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "ClimateSkillSubtropical",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Awls" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
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
                Code = "StandingMilitary",
                DisplayName = "Standing Military",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalWarfare" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "MilitarySocieties",
                DisplayName = "Military Societies",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalWarfare" }
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
                Code = "RaidingParties",
                DisplayName = "Raiding Parties",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalWarfare" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Hunting" },
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
                Code = "ShadeManagement",
                DisplayName = "Shade Management",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillArid" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SlashingBurning",
                DisplayName = "Slashing & Burning",
                Description = "Bonus: Foraging",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                ExperienceRequired = 200,
                ExperienceTypeCodes = ["Extraction.Foraging"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "FireMaking" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HabitatModification" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "IntensiveGathering" }
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
                Code = "TribalProjects",
                DisplayName = "Tribal Projects",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "TribalLeadership" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "WaterConservation",
                DisplayName = "Water Conservation",
                Era = TechnologyEra.Stone,
                DirectedRequired = 400,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillArid" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HandAxe" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "AnimalDomestication",
                DisplayName = "Animal Domestication",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalTaming" }
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
                Code = "BiomeMasteryArid",
                DisplayName = "Biome Mastery - Arid",
                Description = "(on 3rd settlement in the biome; attack bonus on the tile)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Biome.Arid"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeExpertiseArid" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeMasteryForest",
                DisplayName = "Biome Mastery - Forest",
                Description = "(on 3rd settlement in the biome; attack bonus on the tile)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Biome.Forest"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeExpertiseForest" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeMasteryGrassland",
                DisplayName = "Biome Mastery - Grassland",
                Description = "(on 3rd settlement in the biome; attack bonus on the tile)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Biome.Grassland"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeExpertiseGrassland" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeMasteryJungle",
                DisplayName = "Biome Mastery - Jungle",
                Description = "(on 3rd settlement in the biome; attack bonus on the tile)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Biome.Jungle"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeExpertiseJungle" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeMasteryLake",
                DisplayName = "Biome Mastery - Lake",
                Description = "(bonus production)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeExpertiseLake" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeMasteryMangrove",
                DisplayName = "Biome Mastery - Mangrove",
                Description = "(on 2nd settlement in the biome; attack bonus on the tile)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeExpertiseMangrove" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeMasteryWoodland",
                DisplayName = "Biome Mastery - Woodland",
                Description = "(on 3rd settlement in the biome; attack bonus on the tile)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Biome.Woodland"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeExpertiseWoodland" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeSkillDesert",
                DisplayName = "Biome Skill - Desert",
                Description = "(on 1st settlement in the biome)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Biome.Desert"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ShadeManagement" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WaterConservation" }
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
                Code = "Domestication2",
                DisplayName = "Domestication 2",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalTaming" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ForestNative",
                DisplayName = "Forest Native",
                Description = "Gives: Asset Amber",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ForestFamiliarity" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HuntingBirds",
                DisplayName = "Hunting Birds",
                Description = "Asset: Hunting Birds Specialist: Bird Catcher",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                ExperienceRequired = 250,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalTaming" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HuntingBow",
                DisplayName = "Hunting Bow",
                Description = "Weapon: Hunting Bow Asset: Arrows",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "MultiPieceTools",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "RawSinews",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Irrigation",
                DisplayName = "Irrigation",
                Description = "Build: Irrigation Project",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "EarlyFarming" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumScaleConstructionProjects" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ObservationalMethods",
                DisplayName = "Observational Methods",
                Description = "Smart allocation - Diffusion Research",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SeasonalTrade" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PoultryFarming1",
                DisplayName = "Poultry Farming 1",
                Description = "Asset: Birds Action: Domesticate Birds Building: Bird Cages",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalTaming" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HideScrapers" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Knapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ProtectiveClothing",
                DisplayName = "Protective Clothing",
                Description = "Action: Can settle Cold region No damage in cold terrain",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Furs" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LargeGameHunting" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "RegularTrade",
                DisplayName = "Regular Trade",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SeasonalTrade" }
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
                Description = "Weapon: Sling, Specialist: Slinger",
                Era = TechnologyEra.Stone,
                DirectedRequired = 500,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "RawSinews" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Weaving" }
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
                Code = "ArrowNock",
                DisplayName = "Arrow Nock",
                Description = "Bonus: bow damage",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HuntingBow" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HuntingBow" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BambooWhip",
                DisplayName = "Bamboo Whip",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BambooStakes" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "BiomeMasteryDesert",
                DisplayName = "Biome Mastery - Desert",
                Description = "(attack bonus on the tile)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                ExperienceRequired = 300,
                ExperienceTypeCodes = ["Biome.Desert"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BiomeSkillDesert" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HuntingBow" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Canoe",
                DisplayName = "Canoe",
                Description = "Fishing bonus in coastal tiles",
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
                Code = "WarmClothing",
                DisplayName = "Warm Clothing",
                Description = "(on 1st settlement in Tundra)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Furs" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PolarSkill",
                DisplayName = "Polar Skill",
                Description = "(Tundra level 1; on 1st settlement in Tundra)",
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
                Code = "PolarExperience",
                DisplayName = "Polar Experience",
                Description = "(Tundra level 2; on 3rd settlement in Tundra)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PolarSkill" },
                ]
            },
                      new UpsertTechnologyDto
            {
                Code = "Javelin",
                DisplayName = "Javelin",
                Description = "",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Spear" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "DogDomestication",
                DisplayName = "Dog Domestication",
                Description = "Asset: Dogs Dog breeding option",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "MediumGameHunting" }
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
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HuntingBow" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "SmallGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "ForestMastery",
                DisplayName = "Forest Mastery",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ForestNative" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "GoatDomestication",
                DisplayName = "Goat Domestication",
                Description = "Action: Domesticate Goats",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Domestication2" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Hoe",
                DisplayName = "Hoe",
                Description = "Bonus: Gathering",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                ExperienceRequired = 300,
                ExperienceTypeCodes = ["Extraction.Foraging"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Adzes" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HorseDomestication",
                DisplayName = "Horse Domestication",
                Description = "Action: Domesticate Horses",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalDomestication" }
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
                        PrerequisiteTechnologyCode = "HuntingBow",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "Spear",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WoodTools" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "LeadSlingShot",
                DisplayName = "Lead Sling Shot",
                Description = "Ammo: Lead Balls (from Lead) Bonus: Sling damage and range",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Lead" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Sling" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "LongBow",
                DisplayName = "Long Bow",
                Description = "Weapon: Long Bow",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HuntingBow" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Lures",
                DisplayName = "Lures",
                Description = "Bonus: Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                ExperienceRequired = 300,
                ExperienceTypeCodes = ["Extraction.Fishing"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Fishing" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PoultryFarming1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PigDomestication",
                DisplayName = "Pig Domestication",
                Description = "Action: Domesticate Pigs",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Domestication2" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "QuiltedArmour",
                DisplayName = "Quilted Armour",
                Description = "Gives: Quilted Armour",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Awls" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PoultryFarming1" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SheepDomestication",
                DisplayName = "Sheep Domestication",
                Description = "Action: Domesticate Sheep",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Domestication2" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Snares",
                DisplayName = "Snares",
                Description = "Bonus: Hunting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 600,
                ExperienceRequired = 300,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Rope" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Trapping" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PolarExpertise",
                DisplayName = "Polar Expertise",
                Description = "(Tundra level3, Ice level 1; on 1st settlement on Ice)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PolarExperience" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "WarmShelterConstruction" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ProtectiveClothing" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PolarVeteran",
                DisplayName = "Polar Veteran",
                Description = "(Ice level 2, on 3rd settlement on Ice)",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PolarExpertise" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CoastalFamiliarity",
                DisplayName = "Coastal Familiarity",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Canoe" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HabitatFamiliarisation" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Domestication3",
                DisplayName = "Domestication 3",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Domestication2" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LargeGameHunting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HorseBow",
                DisplayName = "Horse Bow",
                Description = "Weapon: Horse Bow",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "ArrowNock" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HorseRiding" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "HuntingDogs",
                DisplayName = "Hunting Dogs",
                Description = "Bonus: Hunting +25% Detection Chassis: Man with Hunting Dog",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                ExperienceRequired = 350,
                ExperienceTypeCodes = ["Extraction.Hunting"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "DogDomestication" }
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
                Code = "Nets",
                DisplayName = "Nets",
                Description = "Bonus Fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                ExperienceRequired = 350,
                ExperienceTypeCodes = ["Extraction.Fishing"],
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Fishing" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Snares" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "OutriggerCanoe",
                DisplayName = "Outrigger Canoe",
                Description = "Bonus: Fishing on coastal tiles",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Canoe" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PersistenceHunting",
                DisplayName = "Persistence Hunting",
                Description = "Bonus: Hunting Training: Endurance (movement bonus)",
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
                Code = "WarCanoe",
                DisplayName = "War Canoe",
                Description = "Unit Design Base: War Canoe",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Canoe" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "WarmShelterConstruction",
                DisplayName = "Warm Shelter Construction",
                Description = "Action: Can create a settlement in a cold region",
                Era = TechnologyEra.Stone,
                DirectedRequired = 700,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "BasicShelterConstruction" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PolarSkill" },
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CamelDomestication",
                DisplayName = "Camel Domestication",
                Description = "Action: Domesticate Camels",
                Era = TechnologyEra.Stone,
                DirectedRequired = 800,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Domestication3" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CattleDomestication",
                DisplayName = "Cattle Domestication",
                Description = "Action: Domesticate Cattle",
                Era = TechnologyEra.Stone,
                DirectedRequired = 800,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Domestication3" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "PolarMastery",
                DisplayName = "Polar Mastery",
                Description = "(Ice level 3, on 5th settlement), ice fishing",
                Era = TechnologyEra.Stone,
                DirectedRequired = 800,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "PolarVeteran" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CoastalNative",
                DisplayName = "Coastal Native",
                Era = TechnologyEra.Stone,
                DirectedRequired = 800,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CoastalFamiliarity" }
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
            },
            new UpsertTechnologyDto
            {
                Code = "LeatherCrafting",
                DisplayName = "Leather Crafting",
                Era = TechnologyEra.Stone,
                DirectedRequired = 800,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LeatherTanning" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "CoastalMastery",
                DisplayName = "Coastal Mastery",
                Era = TechnologyEra.Stone,
                DirectedRequired = 900,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "CoastalNative" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Herding",
                DisplayName = "Herding",
                Description = "+50% domestication birth rate",
                Era = TechnologyEra.Stone,
                DirectedRequired = 900,
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
                Code = "PlowAnimals",
                DisplayName = "Plow Animals",
                Era = TechnologyEra.Stone,
                DirectedRequired = 900,
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
                Code = "Stirrup",
                DisplayName = "Stirrup",
                Description = "Gives: Stirrup",
                Era = TechnologyEra.Stone,
                DirectedRequired = 900,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HorseDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LeatherCrafting" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "TrackingLevel2",
                DisplayName = "Tracking (Level 2)",
                Description = "Bonus: Tracking +100%",
                Era = TechnologyEra.Stone,
                DirectedRequired = 900,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "ClimateMasteryPolar",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "TerrainMasteryHilly",
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
                Code = "AnimalHusbandry",
                DisplayName = "Animal Husbandry",
                Era = TechnologyEra.Stone,
                DirectedRequired = 1_000,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Herding" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "SelectiveBreeding1",
                DisplayName = "Selective Breeding 1",
                Era = TechnologyEra.Stone,
                DirectedRequired = 1_000,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Herding" }
                ]
            },
            new UpsertTechnologyDto
            {
                Code = "Saddle",
                DisplayName = "Saddle",
                Description = "Gives: Saddle",
                Era = TechnologyEra.Stone,
                DirectedRequired = 1_100,
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "AnimalHusbandry" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "HorseDomestication" },
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "LeatherCrafting" }
                ]
            }
        ];
    }
}
