using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>assets</c> schema catalogue applied by the database seed: the meta-categories, categories, groups and Assets a Player's economy draws on.
/// </summary>
/// <remarks>
/// <para>
/// Matched by code, so re-running the seed renames or re-orders a row rather than duplicating it, and never removes one added by hand. Codes are spelled out
/// explicitly at the meta-category, category and group levels because everything below them derives its own code from that prefix - pinning them.
/// </para>
/// <para>
/// Assets mostly leave <c>Code</c> unset and let the upsert derive it from the parent's code and their DisplayName. A graded Asset cannot: every grade of
/// it shares one DisplayName, so all three would derive the same code. Those spell theirs out with the grade suffixed.
/// </para>
/// <para>
/// <b>Icon paths are frontend <c>public</c> URLs, not bundled assets.</b> A path here is written straight to the database and rendered as an
/// <c>&lt;img src&gt;</c>, so it must be root-relative with forward slashes and the file must live under <c>AscendantEmpireUI/public/resources/</c>.
/// </para>
/// <para>
/// <c>StorageScope</c> is left at <c>SettlementBank</c> for everything tangible and stated only where it differs: the two Experience Assets are
/// <c>Player</c>, and every Population and ForeignPopulation Asset is <c>Derived</c>. See <see cref="AssetStorageScope"/> for why that belongs on the
/// catalogue row rather than on the bank.
/// </para>
/// <para>
/// <c>StorageClassCode</c> is stated on every Asset, because there is no default to fall back on - and a class spans the hierarchy freely rather than
/// following it. The storage yard covers two categories and four groups; the fuel dump takes four rows out of the middle of a flat one. See
/// <see cref="StorageClassSeedData"/> for what each class is for.
/// </para>
/// </remarks>
internal static class AssetSeedData
{
    /// <summary>
    /// Every grade a graded Asset is stocked in, in the order the tickers show them.
    /// </summary>
    private static readonly AssetGrade[] _GRADED_RESOURCE_GRADES = [AssetGrade.Low, AssetGrade.Medium, AssetGrade.High];

    /// <summary>
    /// The population artwork, shared by the own-people and foreign-people Assets that mean the same thing.
    /// </summary>
    private const string _CIVILIAN_ICON = "/resources/other/civilian.svg";
    private const string _SLAVE_ICON = "/resources/other/slave.svg";
    private const string _INTERNED_ICON = "/resources/other/interned.svg";
    private const string _PRISONER_OF_WAR_ICON = "/resources/other/prisoner-of-war.svg";

    /// <summary>
    /// The artwork for the three armed services. One per service, and used once each, unlike the population icons above.
    /// </summary>
    private const string _ARMY_ICON = "/resources/other/army.svg";
    private const string _AIRFORCE_ICON = "/resources/other/airforce.svg";
    private const string _NAVY_ICON = "/resources/other/navy.svg";

    /// <summary>
    /// Builds the whole catalogue: meta-categories, their categories and groups, and every Asset within them.
    /// </summary>
    public static IReadOnlyCollection<UpsertAssetMetaCategoryDto> Build()
    {
        return
        [
            BuildConstructionMaterials(),
            BuildWeapons(),
            BuildRawMaterials(),
            BuildFoodstuffs(),
            BuildSociety()
        ];
    }

    private static UpsertAssetMetaCategoryDto BuildConstructionMaterials()
    {
        return new UpsertAssetMetaCategoryDto
        {
            Code = "ConstructionMaterials",
            DisplayName = "Construction Materials",
            IconPath = "/resources/wood/oak-log_40px.png",
            Categories =
            [
                new UpsertAssetCategoryDto
                {
                    Code = "ConstructionMaterials.Wood",
                    DisplayName = "Wood",
                    CategoryType = AssetCategoryType.GroupedAssets,
                    Groups =
                    [
                        new UpsertAssetGroupDto
                        {
                            Code = "ConstructionMaterials.Wood.Logs",
                            DisplayName = "Logs",
                            Assets =
                            [
                                new UpsertAssetDto
                                {
                                    DisplayName = "Oak Log",
                                    IconPath = "/resources/wood/oak-log_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A dense, slow-grown hardwood felled from ancient oak forests. Renowned for its strength and resistance to rot.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Generic Log",
                                    IconPath = "/resources/wood/pine-log_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Logs of mixed or unspecified timber.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Cedar Log",
                                    IconPath = "/resources/wood/cedar-log_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A fragrant, lightweight softwood prized for its natural resistance to insects and moisture.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Ash Log",
                                    IconPath = "/resources/wood/ash-log_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A flexible, tough hardwood cut from ash groves.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Ebony Log",
                                    IconPath = "/resources/wood/ebony-log_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "An exceptionally rare and heavy tropical hardwood.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Bamboo",
                                    IconPath = "/resources/wood/bamboo_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A fast-regenerating grass used as a versatile construction material.",
                                    IsNaturallyOccurring = true
                                }
                            ]
                        },
                        new UpsertAssetGroupDto
                        {
                            Code = "ConstructionMaterials.Wood.Planks",
                            DisplayName = "Planks",
                            Assets =
                            [
                                new UpsertAssetDto
                                {
                                    DisplayName = "Oak Plank",
                                    IconPath = "/resources/wood/oak-plank_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Milled from prime oak logs, these planks provide reliable structural integrity for buildings, warships, and siege equipment."
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Generic Plank",
                                    IconPath = "/resources/wood/pine-plank_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Planks cut from common-grade timber. Adequate for everyday construction needs."
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Cedar Plank",
                                    IconPath = "/resources/wood/cedar-plank_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Planks milled from cedar logs. Lightweight and aromatic, they are popular for lining storage facilities and crafting furniture."
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Ash Plank",
                                    IconPath = "/resources/wood/ash-plank_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Planks produced from ash logs. Used in the production of tool handles, weapon shafts, and lightweight vehicle components."
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Ebony Plank",
                                    IconPath = "/resources/wood/ebony-plank_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Finely milled planks of ebony. Sought after by master craftsmen for decorative inlays, fine furniture, and prestigious buildings."
                                }
                            ]
                        }
                    ]
                },
                new UpsertAssetCategoryDto
                {
                    Code = "ConstructionMaterials.Stone",
                    DisplayName = "Stone",
                    CategoryType = AssetCategoryType.GroupedAssets,
                    Groups =
                    [
                        new UpsertAssetGroupDto
                        {
                            Code = "ConstructionMaterials.Stone.RawRock",
                            DisplayName = "Raw Rock",
                            Assets =
                            [
                                new UpsertAssetDto
                                {
                                    DisplayName = "Limestone",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A sedimentary rock abundant throughout the empire. Limestone is the foundation of basic construction and is crushed to produce mortar and quicklime.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Sandstone",
                                    IconPath = "/resources/stone/sandstone-rocks_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A soft, workable stone gathered from arid regions. Easier to cut than granite, sandstone is commonly used in desert architecture.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Granite",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "An exceptionally hard igneous rock quarried from mountain deposits. Granite resists weathering and is ideal for fortifications and public monuments.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Flint",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A fine-grained sedimentary rock that fractures with a sharp edge. Flint is used in early toolmaking, fire-starting, and as a cheap cutting material.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Obsidian",
                                    IconPath = "/resources/stone/obsidian-pile_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Volcanic glass formed from rapidly cooled lava. Obsidian is razor-sharp when fractured and is prized for specialist blades and ritual objects.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Slate",
                                    IconPath = "/resources/stone/slate-pile_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A fine-grained metamorphic rock that cleaves into thin sheets. Slate is the material of choice for roofing tiles and writing tablets.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Marble",
                                    IconPath = "/resources/stone/marble-pile_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Raw marble extracted from prized quarries. Highly sought after for prestige architecture, sculptures, and civic monuments.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Basalt",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "A dense volcanic rock with superior hardness. Basalt is prized for road paving, millstones, and heavy defensive structures.",
                                    IsNaturallyOccurring = true
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Generic Rock",
                                    IconPath = "/resources/stone/generic-rock-pile_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Rough, unprocessed stone quarried from surface deposits. Used as bulk fill, rubble, and a basic ingredient in early-stage construction.",
                                    IsNaturallyOccurring = true
                                }
                            ]
                        },
                        new UpsertAssetGroupDto
                        {
                            Code = "ConstructionMaterials.Stone.Bricks",
                            DisplayName = "Bricks",
                            Assets =
                            [
                                new UpsertAssetDto
                                {
                                    DisplayName = "Sandstone Block",
                                    IconPath = "/resources/stone/sandstone-block_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Dressed sandstone blocks shaped for construction. Widely used in the southern provinces for walls, temples, and public buildings."
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Marble Block",
                                    IconPath = "/resources/stone/marble-block_40px.png",
                                    StorageClassCode = StorageClassSeedData.STORAGE_YARD,
                                    Description = "Cut and dressed marble blocks, ready for use in grand construction projects. A symbol of wealth and refined taste."
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static UpsertAssetMetaCategoryDto BuildWeapons()
    {
        return new UpsertAssetMetaCategoryDto
        {
            Code = "Weapons",
            DisplayName = "Weapons",
            IconPath = "/resources/unit-design/weapons/melee/stone-axe_40px.png",
            Categories =
            [
                new UpsertAssetCategoryDto
                {
                    Code = "Weapons.MeleeWeapons",
                    DisplayName = "Melee Weapons",
                    CategoryType = AssetCategoryType.StandaloneAssets,
                    Assets =
                    [
                        .. GradedAsset(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "BoneKnife",
                            displayName: "Bone Knife",
                            iconPath: "/resources/unit-design/weapons/melee/bone-knife_40px.png",
                            description: "A knife worked from bone, treated and shaped to hold an edge. Cheap to produce and easy to replace, and surprisingly effective for such a primitive material."),
                        .. GradedAsset(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "WoodClub",
                            displayName: "Wood Club",
                            iconPath: "/resources/unit-design/weapons/melee/wood-club_40px.png",
                            description: "A bludgeoning weapon cut from dense hardwood, weighted towards the striking head. One of the oldest weapons known, and still a staple of poorly-equipped forces."),
                        .. GradedAsset(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "TwoPieceWoodSpear",
                            displayName: "Two-Piece Wood Spear",
                            iconPath: "/resources/unit-design/weapons/melee/two-piece-wood-spear_40px.png",
                            description: "A thrusting weapon assembled from two shaped wooden shafts. The modular design allows easy transport and field repair, and it is reliable in formation combat."),
                        .. GradedAsset(
                            categoryCode: "Weapons.MeleeWeapons",
                            codeStem: "StoneAxe",
                            displayName: "Stone Axe",
                            iconPath: "/resources/unit-design/weapons/melee/stone-axe_40px.png",
                            description: "A heavy chopping weapon with a stone head bound to a wooden haft. Predates metalworking and is still found wherever iron is scarce."),
                        .. GradedAsset(
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
    // Raw Materials
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static UpsertAssetMetaCategoryDto BuildRawMaterials()
    {
        return new UpsertAssetMetaCategoryDto
        {
            Code = "RawMaterials",
            DisplayName = "Raw Materials",
            IconPath = "/resources/other/fishbone.svg",
            Categories =
            [
                new UpsertAssetCategoryDto
                {
                    Code = "RawMaterials.RawMaterials",
                    DisplayName = "Raw Materials",
                    CategoryType = AssetCategoryType.StandaloneAssets,
                    Assets =
                    [
                        new UpsertAssetDto
                        {
                            DisplayName = "Bone",
                            IconPath = "/resources/other/fishbone.svg",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Bones collected from slaughterhouses and hunting camps. Processed into tools, needles, fertiliser, and animal glue.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Fodder",
                            IconPath = "/resources/other/high-grass.svg",
                            StorageClassCode = StorageClassSeedData.GRANARY,
                            Description = "Dried grasses, hay, and grain husks used to sustain livestock and working animals. A steady supply is essential for any mounted force or agricultural operation.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Ochre",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "A naturally occurring iron oxide pigment. Ochre is used in dyes, paints, and pottery glazes throughout the empire.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Chalk",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "A soft white limestone used as a writing material, in agriculture to neutralise acidic soils, and as a base for certain pigments.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Clay",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Raw earthen material gathered from riverbeds and mineral deposits. Clay is the primary ingredient in pottery, bricks, and ceramic tiles.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Salt",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "A vital mineral harvested from salt flats and coastal evaporation pools. Salt is essential for food preservation, leather tanning, and chemical processes.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Sulfur",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "A yellow mineral collected from volcanic vents and mineral springs. Sulfur is a key ingredient in gunpowder, medicinal compounds, and fumigation.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Pitch",
                            StorageClassCode = StorageClassSeedData.FUEL_DUMP,
                            Description = "A thick, dark tar extracted from pine trees or coal deposits. Pitch waterproofs ships and barrels, seals building joints, and fuels incendiary weapons."
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Flax",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "A fibrous plant cultivated for its stems and seeds. Flax is processed into linen cloth, rope, and linseed oil.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Resin",
                            StorageClassCode = StorageClassSeedData.FUEL_DUMP,
                            Description = "Sticky sap harvested from conifer trees. Resin is used in varnishes, adhesives, medicines, and as a flux in metalworking.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Tar",
                            StorageClassCode = StorageClassSeedData.FUEL_DUMP,
                            Description = "A dense, viscous liquid derived from the destructive distillation of wood or coal. Tar preserves timber, waterproofs hulls, and serves as a base for road surfaces."
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Leather",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Cured animal hides processed into a durable and flexible material. Leather is indispensable for armour, harnesses, footwear, and bookbinding."
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Hides",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Raw, uncured animal skins awaiting processing. Hides are the primary input for the leather-working industry.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Tallow",
                            StorageClassCode = StorageClassSeedData.FUEL_DUMP,
                            Description = "Animal fat rendered from slaughtered livestock. Tallow is used in candle-making, soap production, and as a lubricant for machinery and weapons."
                        }
                    ]
                }
            ]
        };
    }

    private static UpsertAssetMetaCategoryDto BuildFoodstuffs()
    {
        return new UpsertAssetMetaCategoryDto
        {
            Code = "Foodstuffs",
            DisplayName = "Foodstuffs",

            // Shares the Fodder artwork rather than carrying one of its own - the closest thing in the set to "something edible gathered off the land".
            // Food artwork can be dropped in later without disturbing anything below it.
            IconPath = "/resources/other/high-grass.svg",
            Categories =
            [
                new UpsertAssetCategoryDto
                {
                    Code = "Foodstuffs.Meat",
                    DisplayName = "Meat",
                    CategoryType = AssetCategoryType.StandaloneAssets,
                    Assets =
                    [
                        new UpsertAssetDto
                        {
                            DisplayName = "Game",
                            IconPath = "/resources/food/meat-02_40px.png",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "The flesh of hunted land animals.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Birds",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Wildfowl taken with net, snare or sling.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Fish",
                            IconPath = "/resources/food/fish_40px.png",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Freshwater and sea fish.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Shellfish",
                            IconPath = "/resources/food/crab_40px.png",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Mussels, clams, oysters and crabs prised off the tideline.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Insects",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Grubs, locusts, termites and ant brood collected in quantity.",
                            IsNaturallyOccurring = true
                        }
                    ]
                },
                new UpsertAssetCategoryDto
                {
                    Code = "Foodstuffs.PlantFoods",
                    DisplayName = "Plant Foods",
                    CategoryType = AssetCategoryType.StandaloneAssets,
                    Assets =
                    [
                        new UpsertAssetDto
                        {
                            DisplayName = "Fruit",
                            IconPath = "/resources/food/apple_40px.png",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Tree and vine fruit picked ripe - apples, figs, plums and dates.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Berries",
                            IconPath = "/resources/food/berries_40px.png",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Hedgerow and scrubland berries stripped from the bush.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Roots",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Edible taproots dug out of open ground.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Tubers",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Starchy underground swellings - yams, water chestnuts and their like.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Greens",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Wild leaves, shoots and stalks cut young.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Mushrooms",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Fungi gathered from damp woodland and pasture.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Bulbs",
                            StorageClassCode = StorageClassSeedData.LARDER,
                            Description = "Wild onions, garlic and lily bulbs lifted whole.",
                            IsNaturallyOccurring = true
                        }
                    ]
                },
                new UpsertAssetCategoryDto
                {
                    Code = "Foodstuffs.Staples",
                    DisplayName = "Staples",
                    CategoryType = AssetCategoryType.StandaloneAssets,
                    Assets =
                    [
                        new UpsertAssetDto
                        {
                            DisplayName = "Wild Grains",
                            StorageClassCode = StorageClassSeedData.GRANARY,
                            Description = "Seed heads beaten from wild stands of emmer, barley and rice.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Seeds",
                            StorageClassCode = StorageClassSeedData.GRANARY,
                            Description = "Oil-rich seeds winnowed from grasses and flowering plants.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Legumes",
                            StorageClassCode = StorageClassSeedData.GRANARY,
                            Description = "Wild peas, beans and vetch shelled from the pod.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Nuts",
                            StorageClassCode = StorageClassSeedData.GRANARY,
                            Description = "Acorns, hazelnuts, walnuts and pine nuts gathered at the autumn fall.",
                            IsNaturallyOccurring = true
                        }
                    ]
                },
                new UpsertAssetCategoryDto
                {
                    Code = "Foodstuffs.SpecialFoods",
                    DisplayName = "Special Foods",
                    CategoryType = AssetCategoryType.StandaloneAssets,
                    Assets =
                    [
                        new UpsertAssetDto
                        {
                            DisplayName = "Honey",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Comb raided from wild hives in hollow trees and cliff faces.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Seaweed",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Kelp and laver cut off the rocks at low tide and dried on frames.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Chilli Peppers",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Chilli peppers gathered from the wild. Spicy and pungent, they add heat to any dish.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Wild Herbs",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Aromatic and medicinal plants cut where they grow.",
                            IsNaturallyOccurring = true
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Poisons",
                            StorageClassCode = StorageClassSeedData.SUNDRIES,
                            Description = "Toxic substances derived from plants, animals, or minerals.",
                            IsNaturallyOccurring = true
                        },
                    ]
                }
            ]
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Society
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static UpsertAssetMetaCategoryDto BuildSociety()
    {
        return new UpsertAssetMetaCategoryDto
        {
            Code = "Society",
            DisplayName = "Society",

            // The toolbar button for the whole meta-category. It shares the Civilians icon rather than having one of its own, which is the closest
            // thing to "a people" in the set.
            IconPath = _CIVILIAN_ICON,
            Categories =
            [
                new UpsertAssetCategoryDto
                {
                    Code = "Society.Experience",
                    DisplayName = "Experience",
                    CategoryType = AssetCategoryType.StandaloneAssets,

                    // Both are held per Player for the whole game rather than in any settlement's bank, and the figures behind them already exist:
                    // PlayerResearchState.UnspentDirectedPoints and UnspentDiffusionPoints, plus PlayerUnusedExperience for the typed-per-kind remainder.
                    // Giving either a balance row would make a second source of truth for one number, with the research pipeline writing the one the panel
                    // is not reading.
                    Assets =
                    [
                        new UpsertAssetDto
                        {
                            DisplayName = "Technology",
                            IconPath = "/resources/other/bubbling-flask.svg",
                            StorageClassCode = StorageClassSeedData.INTANGIBLE,
                            Description = "",
                            StorageScope = AssetStorageScope.Player
                        },
                        new UpsertAssetDto
                        {
                            DisplayName = "Culture",
                            IconPath = "/resources/other/lyre.svg",
                            StorageClassCode = StorageClassSeedData.INTANGIBLE,
                            Description = "",
                            StorageScope = AssetStorageScope.Player
                        },
                    ]
                },
                new UpsertAssetCategoryDto
                {
                    Code = "Society.Population",
                    DisplayName = "My Population",
                    CategoryType = AssetCategoryType.GroupedAssets,
                    Groups =
                    [
                        new UpsertAssetGroupDto
                        {
                            Code = "Society.Population.MyPeople",
                            DisplayName = "My People",

                            // Nobody banks people. Civilians comes from Settlement.Population, and Slaves / Interned / P.o.W. from the holdings ledger
                            // RS-20 specified and did not build - so these are derived at read time rather than stored anywhere.
                            Assets =
                            [
                                new UpsertAssetDto
                                {
                                    DisplayName = "Civilians",
                                    IconPath = _CIVILIAN_ICON,
                                    StorageClassCode = StorageClassSeedData.PEOPLE,
                                    Description = "Civilians in our Faction",
                                    StorageScope = AssetStorageScope.Derived
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Slaves",
                                    IconPath = _SLAVE_ICON,
                                    StorageClassCode = StorageClassSeedData.PEOPLE,
                                    Description = "Our people held as slaves",
                                    StorageScope = AssetStorageScope.Derived
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Interned",
                                    IconPath = _INTERNED_ICON,
                                    StorageClassCode = StorageClassSeedData.PEOPLE,
                                    Description = "Our people interned in camps",
                                    StorageScope = AssetStorageScope.Derived
                                },
                                new UpsertAssetDto
                                {
                                    // Spelled out rather than derived: ToProperCode would turn the display name into "PrisonersOfWar", and the starter
                                    // assets a new game is given name this one "PoW". Game setup refuses to run without it.
                                    Code = "Society.Population.MyPeople.PoW",
                                    DisplayName = "Prisoners of War",
                                    IconPath = _PRISONER_OF_WAR_ICON,
                                    StorageClassCode = StorageClassSeedData.PEOPLE,
                                    Description = "Our forces rotting as Prisoners of War",
                                    StorageScope = AssetStorageScope.Derived
                                },
                            ]
                        },
                        new UpsertAssetGroupDto
                        {
                            Code = "Society.Population.Forces",
                            DisplayName = "Forces",

                            // Counted off the force rosters, which already know who is serving where.
                            Assets =
                            [
                                new UpsertAssetDto
                                {
                                    DisplayName = "Army",
                                    IconPath = _ARMY_ICON,
                                    StorageClassCode = StorageClassSeedData.INTANGIBLE,
                                    Description = "Members of this people serving under arms on land.",
                                    StorageScope = AssetStorageScope.Derived
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Airforce",
                                    IconPath = _AIRFORCE_ICON,
                                    StorageClassCode = StorageClassSeedData.INTANGIBLE,
                                    Description = "Members of this people serving in the air.",
                                    StorageScope = AssetStorageScope.Derived
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Navy",
                                    IconPath = _NAVY_ICON,
                                    StorageClassCode = StorageClassSeedData.INTANGIBLE,
                                    Description = "Members of this people serving at sea.",
                                    StorageScope = AssetStorageScope.Derived
                                },
                            ]
                        },
                    ]
                },
                new UpsertAssetCategoryDto
                {
                    Code = "Society.ForeignPopulation",
                    DisplayName = "Enemies",
                    CategoryType = AssetCategoryType.GroupedAssets,

                    // The category is shown under its own name; its groups are the templates the per-player columns are stamped from, each labelled
                    // with that Player's people name rather than the group's own - see the banner above AssetSubjectScope, and the projection in
                    // GetPlayerAssetsHandler.
                    SubjectScope = AssetSubjectScope.PerOtherPlayer,
                    Groups =
                    [
                        new UpsertAssetGroupDto
                        {
                            Code = "Society.ForeignPopulation.Subjugated",
                            DisplayName = "Subjugated",

                            // Derived for the same reason as their counterparts under My People: the figure comes from the holdings ledger, and these are
                            // the rows it is read per subject Player for.
                            Assets =
                            [
                                new UpsertAssetDto
                                {
                                    DisplayName = "Slaves",
                                    IconPath = _SLAVE_ICON,
                                    StorageClassCode = StorageClassSeedData.PEOPLE,
                                    Description = "People we have enslaved",
                                    StorageScope = AssetStorageScope.Derived
                                },
                                new UpsertAssetDto
                                {
                                    DisplayName = "Interned",
                                    IconPath = _INTERNED_ICON,
                                    StorageClassCode = StorageClassSeedData.PEOPLE,
                                    Description = "Civilians we have interned",
                                    StorageScope = AssetStorageScope.Derived
                                },
                                new UpsertAssetDto
                                {
                                    // Spelled out for the same reason as its counterpart under My People.
                                    Code = "Society.ForeignPopulation.Subjugated.PoW",
                                    DisplayName = "Prisoners of War",
                                    IconPath = _PRISONER_OF_WAR_ICON,
                                    StorageClassCode = StorageClassSeedData.PEOPLE,
                                    Description = "Enemy combatants kept as Prisoners of War",
                                    StorageScope = AssetStorageScope.Derived
                                },
                            ]
                        },
                    ]
                },
            ]
        };
    }

    /// <summary>
    /// Expands one graded Asset into the row per grade the catalogue stocks it in.
    /// </summary>
    /// <remarks>
    /// Every grade shares the DisplayName, the icon and the description - the grade is a quality of the same thing, not a different thing - so the code is what
    /// tells them apart and has to be spelled out rather than derived.
    /// </remarks>
    /// <param name="codeStem">The Asset's own ProperCase code segment, e.g. "BoneKnife". The grade is appended to it.</param>
    private static IEnumerable<UpsertAssetDto> GradedAsset(
        string categoryCode,
        string codeStem,
        string displayName,
        string iconPath,
        string description)
    {
        // The armoury is hardcoded because every graded Asset in the catalogue is a weapon. A graded Asset of some other class would need the code passed in.
        return _GRADED_RESOURCE_GRADES.Select(grade => new UpsertAssetDto
        {
            Code = $"{categoryCode}.{codeStem}.{grade}",
            DisplayName = displayName,
            IconPath = iconPath,
            StorageClassCode = StorageClassSeedData.ARMOURY,
            Description = description,
            IsGradedAsset = true,
            AssetGrade = grade
        });
    }
}
