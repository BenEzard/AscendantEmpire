using AscendantEmpire.BackendContracts.Resources;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>resources</c> schema catalogue applied by the database seed: the meta-categories, categories, groups and Resources a Player's economy draws on.
/// </summary>
/// <remarks>
/// <para>
/// Matched by code, so re-running the seed renames or re-orders a row rather than duplicating it, and never removes one added by hand. Codes are spelled out
/// explicitly at the meta-category, category and group levels because everything below them derives its own code from that prefix - pinning them keeps a
/// rename of a DisplayName from re-keying the whole subtree.
/// </para>
/// <para>
/// Resources mostly leave <c>Code</c> unset and let the upsert derive it from the parent's code and their DisplayName. A graded Resource cannot: every grade of
/// it shares one DisplayName, so all three would derive the same code. Those spell theirs out with the grade suffixed.
/// </para>
/// <para>
/// SortOrder is left unset throughout and derived from list position, spaced by 10, so the order things appear in this file is the order they appear in game
/// and there is no second set of numbers to keep in step with it.
/// </para>
/// <para>
/// <b>Icon paths are frontend <c>public</c> URLs, not bundled assets.</b> A path here is written straight to the database and rendered as an
/// <c>&lt;img src&gt;</c>, so it must be root-relative with forward slashes and the file must live under <c>AscendantEmpireUI/public/resources/</c>.
/// </para>
/// <para>
/// <c>NeverLuxury</c> is left at its default everywhere. It says a Resource can never be classified a luxury however scarce it gets, which is a balance
/// decision belonging with the luxury classification work rather than something this catalogue should invent.
/// </para>
/// </remarks>
internal static class ResourceSeedData
{
    /// <summary>
    /// Every grade a graded Resource is stocked in, in the order the tickers show them.
    /// </summary>
    private static readonly ResourceGrade[] _GRADED_RESOURCE_GRADES = [ResourceGrade.Low, ResourceGrade.Medium, ResourceGrade.High];

    /// <summary>
    /// Builds the whole catalogue: three meta-categories, their categories and groups, and every Resource within them.
    /// </summary>
    public static IReadOnlyCollection<UpsertResourceMetaCategoryDto> Build()
    {
        return
        [
            BuildConstructionMaterials(),
            BuildWeapons(),
            BuildRawMaterials()
        ];
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Construction Materials - timber and stone, both grouped so the tickers show a labelled row per group.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static UpsertResourceMetaCategoryDto BuildConstructionMaterials()
    {
        return new UpsertResourceMetaCategoryDto
        {
            Code = "ConstructionMaterials",
            DisplayName = "Construction Materials",
            IconPath = "/resources/wood/oak-log_40px.png",
            Categories =
            [
                new UpsertResourceCategoryDto
                {
                    Code = "ConstructionMaterials.Wood",
                    DisplayName = "Wood",
                    CategoryType = ResourceCategoryType.GroupedResources,
                    Groups =
                    [
                        new UpsertResourceGroupDto
                        {
                            Code = "ConstructionMaterials.Wood.Logs",
                            DisplayName = "Logs",
                            Resources =
                            [
                                new UpsertResourceDto
                                {
                                    DisplayName = "Oak Log",
                                    IconPath = "/resources/wood/oak-log_40px.png",
                                    Description = "A dense, slow-grown hardwood felled from ancient oak forests. Renowned for its strength and resistance to rot, oak logs are a cornerstone of construction and military engineering alike."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Generic Log",
                                    IconPath = "/resources/wood/pine-log_40px.png",
                                    Description = "Logs of mixed or unspecified timber, sourced from managed woodland reserves. Serviceable for fuel, basic construction, and rough carpentry."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Cedar Log",
                                    IconPath = "/resources/wood/cedar-log_40px.png",
                                    Description = "A fragrant, lightweight softwood prized for its natural resistance to insects and moisture. Cedar logs are favored for interior fittings and storage chests."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Ash Log",
                                    IconPath = "/resources/wood/ash-log_40px.png",
                                    Description = "A flexible, tough hardwood cut from ash groves. Ash logs are particularly valued by weaponsmiths and wheelwrights for their shock-absorbing properties."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Ebony Log",
                                    IconPath = "/resources/wood/ebony-log_40px.png",
                                    Description = "An exceptionally rare and heavy tropical hardwood. Ebony logs command premium prices on the open market and are reserved for luxury goods and high-status construction."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Bamboo",
                                    IconPath = "/resources/wood/bamboo_40px.png",
                                    Description = "A fast-regenerating grass used as a versatile construction material. Bamboo is lightweight, surprisingly strong, and grows abundantly in tropical regions."
                                }
                            ]
                        },
                        new UpsertResourceGroupDto
                        {
                            Code = "ConstructionMaterials.Wood.Planks",
                            DisplayName = "Planks",
                            Resources =
                            [
                                new UpsertResourceDto
                                {
                                    DisplayName = "Oak Plank",
                                    IconPath = "/resources/wood/oak-plank_40px.png",
                                    Description = "Milled from prime oak logs, these planks provide reliable structural integrity for buildings, warships, and siege equipment. Widely traded across the empire."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Generic Plank",
                                    IconPath = "/resources/wood/pine-plank_40px.png",
                                    Description = "Planks cut from common-grade timber. Adequate for everyday construction needs, though less prized than specialty hardwoods."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Cedar Plank",
                                    IconPath = "/resources/wood/cedar-plank_40px.png",
                                    Description = "Planks milled from cedar logs. Lightweight and aromatic, they are popular for lining storage facilities and crafting furniture."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Ash Plank",
                                    IconPath = "/resources/wood/ash-plank_40px.png",
                                    Description = "Planks produced from ash logs. Used in the production of tool handles, weapon shafts, and lightweight vehicle components."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Ebony Plank",
                                    IconPath = "/resources/wood/ebony-plank_40px.png",
                                    Description = "Finely milled planks of ebony. Sought after by master craftsmen for decorative inlays, fine furniture, and prestigious buildings."
                                }
                            ]
                        }
                    ]
                },
                new UpsertResourceCategoryDto
                {
                    Code = "ConstructionMaterials.Stone",
                    DisplayName = "Stone",
                    CategoryType = ResourceCategoryType.GroupedResources,
                    Groups =
                    [
                        new UpsertResourceGroupDto
                        {
                            Code = "ConstructionMaterials.Stone.RawRock",
                            DisplayName = "Raw Rock",
                            Resources =
                            [
                                new UpsertResourceDto
                                {
                                    DisplayName = "Limestone",
                                    Description = "A sedimentary rock abundant throughout the empire. Limestone is the foundation of basic construction and is crushed to produce mortar and quicklime."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Sandstone",
                                    IconPath = "/resources/stone/sandstone-rocks_40px.png",
                                    Description = "A soft, workable stone gathered from arid regions. Easier to cut than granite, sandstone is commonly used in desert architecture."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Granite",
                                    Description = "An exceptionally hard igneous rock quarried from mountain deposits. Granite resists weathering and is ideal for fortifications and public monuments."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Flint",
                                    Description = "A fine-grained sedimentary rock that fractures with a sharp edge. Flint is used in early toolmaking, fire-starting, and as a cheap cutting material."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Obsidian",
                                    IconPath = "/resources/stone/obsidian-pile_40px.png",
                                    Description = "Volcanic glass formed from rapidly cooled lava. Obsidian is razor-sharp when fractured and is prized for specialist blades and ritual objects."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Slate",
                                    IconPath = "/resources/stone/slate-pile_40px.png",
                                    Description = "A fine-grained metamorphic rock that cleaves into thin sheets. Slate is the material of choice for roofing tiles and writing tablets."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Marble",
                                    IconPath = "/resources/stone/marble-pile_40px.png",
                                    Description = "Raw marble extracted from prized quarries. Highly sought after for prestige architecture, sculptures, and civic monuments."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Basalt",
                                    Description = "A dense volcanic rock with superior hardness. Basalt is prized for road paving, millstones, and heavy defensive structures."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Generic Rock",
                                    IconPath = "/resources/stone/generic-rock-pile_40px.png",
                                    Description = "Rough, unprocessed stone quarried from surface deposits. Used as bulk fill, rubble, and a basic ingredient in early-stage construction."
                                }
                            ]
                        },
                        new UpsertResourceGroupDto
                        {
                            Code = "ConstructionMaterials.Stone.Bricks",
                            DisplayName = "Bricks",
                            Resources =
                            [
                                new UpsertResourceDto
                                {
                                    DisplayName = "Sandstone Block",
                                    IconPath = "/resources/stone/sandstone-block_40px.png",
                                    Description = "Dressed sandstone blocks shaped for construction. Widely used in the southern provinces for walls, temples, and public buildings."
                                },
                                new UpsertResourceDto
                                {
                                    DisplayName = "Marble Block",
                                    IconPath = "/resources/stone/marble-block_40px.png",
                                    Description = "Cut and dressed marble blocks, ready for use in grand construction projects. A symbol of wealth and refined taste."
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Weapons - every melee weapon is graded, so each contributes one Resource per grade rather than a single row.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static UpsertResourceMetaCategoryDto BuildWeapons()
    {
        return new UpsertResourceMetaCategoryDto
        {
            Code = "Weapons",
            DisplayName = "Weapons",
            IconPath = "/resources/unit-design/weapons/melee/stone-axe_40px.png",
            Categories =
            [
                new UpsertResourceCategoryDto
                {
                    Code = "Weapons.MeleeWeapons",
                    DisplayName = "Melee Weapons",
                    CategoryType = ResourceCategoryType.StandaloneResources,
                    Resources =
                    [
                        .. GradedResource(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "BoneKnife",
                            displayName: "Bone Knife",
                            iconPath: "/resources/unit-design/weapons/melee/bone-knife_40px.png",
                            description: "A knife worked from bone, treated and shaped to hold an edge. Cheap to produce and easy to replace, and surprisingly effective for such a primitive material."),
                        .. GradedResource(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "WoodClub",
                            displayName: "Wood Club",
                            iconPath: "/resources/unit-design/weapons/melee/wood-club_40px.png",
                            description: "A bludgeoning weapon cut from dense hardwood, weighted towards the striking head. One of the oldest weapons known, and still a staple of poorly-equipped forces."),
                        .. GradedResource(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "TwoPieceWoodSpear",
                            displayName: "Two-Piece Wood Spear",
                            iconPath: "/resources/unit-design/weapons/melee/two-piece-wood-spear_40px.png",
                            description: "A thrusting weapon assembled from two shaped wooden shafts. The modular design allows easy transport and field repair, and it is reliable in formation combat."),
                        .. GradedResource(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "StoneAxe",
                            displayName: "Stone Axe",
                            iconPath: "/resources/unit-design/weapons/melee/stone-axe_40px.png",
                            description: "A heavy chopping weapon with a stone head bound to a wooden haft. Predates metalworking and is still found wherever iron is scarce."),
                        .. GradedResource(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "MedievalSword",
                            displayName: "Medieval Sword",
                            iconPath: "/resources/unit-design/weapons/melee/medieval-sword_40px.png",
                            description: "A straight-bladed cutting and thrusting weapon of sophisticated design. Requires skilled smiths and quality materials, marking it a step above primitive arms.")
                    ]
                }
            ]
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Raw Materials - a flat list for now. Splitting it into animal products, pigments and fibres is a design decision left for later; because the seed is
    // code-matched, regrouping these later re-parents the existing rows rather than duplicating them.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static UpsertResourceMetaCategoryDto BuildRawMaterials()
    {
        return new UpsertResourceMetaCategoryDto
        {
            Code = "RawMaterials",
            DisplayName = "Raw Materials",
            IconPath = "/resources/other/fishbone.svg",
            Categories =
            [
                new UpsertResourceCategoryDto
                {
                    Code = "RawMaterials.RawMaterials",
                    DisplayName = "Raw Materials",
                    CategoryType = ResourceCategoryType.StandaloneResources,
                    Resources =
                    [
                        new UpsertResourceDto
                        {
                            DisplayName = "Bone",
                            IconPath = "/resources/other/fishbone.svg",
                            Description = "Bones collected from slaughterhouses and hunting camps. Processed into tools, needles, fertiliser, and animal glue."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Fodder",
                            IconPath = "/resources/other/high-grass.svg",
                            Description = "Dried grasses, hay, and grain husks used to sustain livestock and working animals. A steady supply is essential for any mounted force or agricultural operation."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Ochre",
                            Description = "A naturally occurring iron oxide pigment. Ochre is used in dyes, paints, and pottery glazes throughout the empire."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Chalk",
                            Description = "A soft white limestone used as a writing material, in agriculture to neutralise acidic soils, and as a base for certain pigments."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Clay",
                            Description = "Raw earthen material gathered from riverbeds and mineral deposits. Clay is the primary ingredient in pottery, bricks, and ceramic tiles."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Salt",
                            Description = "A vital mineral harvested from salt flats and coastal evaporation pools. Salt is essential for food preservation, leather tanning, and chemical processes."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Sulfur",
                            Description = "A yellow mineral collected from volcanic vents and mineral springs. Sulfur is a key ingredient in gunpowder, medicinal compounds, and fumigation."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Pitch",
                            Description = "A thick, dark tar extracted from pine trees or coal deposits. Pitch waterproofs ships and barrels, seals building joints, and fuels incendiary weapons."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Flax",
                            Description = "A fibrous plant cultivated for its stems and seeds. Flax is processed into linen cloth, rope, and linseed oil."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Resin",
                            Description = "Sticky sap harvested from conifer trees. Resin is used in varnishes, adhesives, medicines, and as a flux in metalworking."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Tar",
                            Description = "A dense, viscous liquid derived from the destructive distillation of wood or coal. Tar preserves timber, waterproofs hulls, and serves as a base for road surfaces."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Leather",
                            Description = "Cured animal hides processed into a durable and flexible material. Leather is indispensable for armour, harnesses, footwear, and bookbinding."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Hides",
                            Description = "Raw, uncured animal skins awaiting processing. Hides are the primary input for the leather-working industry."
                        },
                        new UpsertResourceDto
                        {
                            DisplayName = "Tallow",
                            Description = "Animal fat rendered from slaughtered livestock. Tallow is used in candle-making, soap production, and as a lubricant for machinery and weapons."
                        }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Expands one graded Resource into the row per grade the catalogue stocks it in.
    /// </summary>
    /// <remarks>
    /// Every grade shares the DisplayName, the icon and the description - the grade is a quality of the same thing, not a different thing - so the code is what
    /// tells them apart and has to be spelled out rather than derived.
    /// </remarks>
    /// <param name="codeStem">The Resource's own ProperCase code segment, e.g. "BoneKnife". The grade is appended to it.</param>
    private static IEnumerable<UpsertResourceDto> GradedResource(
        string categoryCode,
        string codeStem,
        string displayName,
        string iconPath,
        string description)
    {
        return _GRADED_RESOURCE_GRADES.Select(grade => new UpsertResourceDto
        {
            Code = $"{categoryCode}.{codeStem}.{grade}",
            DisplayName = displayName,
            IconPath = iconPath,
            Description = description,
            IsGradedResource = true,
            ResourceGrade = grade
        });
    }
}
