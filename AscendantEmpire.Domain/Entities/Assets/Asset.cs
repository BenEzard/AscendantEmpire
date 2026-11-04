using AscendantEmpire.Domain.Enums.Assets;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// The actual asset, e.g. Oak, Marble, Coal, Small Game Meat.
/// </summary>
public sealed class Asset
{
    /// <summary>
    /// Codes are hierarchical, so the limit has to hold a full dotted path rather than one segment - "ConstructionMaterials.Stone.Bricks.SandstoneBlock" is already 49.
    /// </summary>
    public const int MAX_CODE_LENGTH = 100;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;
    public const int MAX_ICON_PATH_LENGTH = 500;
    public const int MAX_DESCRIPTION_LENGTH = 1000;

    public int Id { get; set; }

    /// <summary>
    /// Unique stable key, hierarchical from the meta-category down, e.g. "ConstructionMaterials.Wood.Logs.OakLog".
    /// </summary>
    /// <remarks>
    /// What every upsert matches on, so it is what makes re-seeding the catalogue safe: a row whose DisplayName changes but whose code does not is renamed
    /// rather than duplicated. A graded Asset suffixes the grade, since all its grades share one DisplayName.
    /// </remarks>
    [MaxLength(MAX_CODE_LENGTH), Required]
    public required string Code { get; set; } = null!;

    [MaxLength(MAX_DISPLAY_NAME_LENGTH), Required]
    public required string DisplayName { get; set; } = null!;

    /// <summary>
    /// Flavour text describing what this Asset is and what it is used for.
    /// </summary>
    /// <remarks>Shown on the Asset Manager's General page. Null where none has been written yet, which the UI substitutes a placeholder for.</remarks>
    [MaxLength(MAX_DESCRIPTION_LENGTH)]
    public string? Description { get; set; }


    /// <summary>
    /// Gets or sets the file path of the icon.
    /// </summary>
    [MaxLength(MAX_ICON_PATH_LENGTH), Required]
    public required string IconPath { get; set; } = null!;

    /// <summary>
    /// Is this Asset graded? (Low, Medium, High)
    /// </summary>
    /// <remarks>
    /// <para>Note that this means if IsGradedAsset is false, AssetGrade should be None.</para>
    /// <para>If IsGradedAsset is true, there will be Assets with each grade (Low, Medium, High).</para>
    /// </remarks>
    public required bool IsGradedAsset { get; set; }

    /// <summary>
    /// The grade of this Asset if it is a graded asset. If IsGradedAsset is false, this should be None.
    /// </summary>
    public AssetGrade AssetGrade { get; set; }

    /// <summary>
    /// Display order of this Asset within its Group or Category.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The global default only. A Player can rearrange their own tickers, and that per-Player ordering takes precedence over this once it exists; until then
    /// this is the order everyone sees, and it is what stops a Player with no ordering of their own getting an arbitrary one.
    /// </para>
    /// <para>Spaced by 10 by the seed, so an Asset can be slotted between two others without renumbering the rest.</para>
    /// </remarks>
    public required int SortOrder { get; set; }

    /// <summary>
    /// Asset always belongs to a meta category.
    /// </summary>
    public int AssetMetaCategoryId { get; set; }

    /// <summary>
    /// Set when this asset belongs to a <see cref="AssetGroup"/> (grouped path).
    /// Mutually exclusive with <see cref="AssetCategoryId"/>.
    /// </summary>
    public int? AssetGroupId { get; set; }

    public AssetGroup? Group { get; set; }

    /// <summary>
    /// Set when this asset belongs directly to a <see cref="AssetCategory"/>
    /// whose <c>CategoryType</c> is <c>StandaloneAssets</c> (standalone path).
    /// Mutually exclusive with <see cref="AssetGroupId"/>.
    /// </summary>
    public int? AssetCategoryId { get; set; }

    public AssetCategory? Category { get; set; }

    /// <summary>
    /// If true, this Asset will never be treated as a luxury regardless of supply.
    /// </summary>
    public bool NeverLuxury { get; set; }

    /// <summary>
    /// Does this Asset occur in the world without human intervention, so a tile can yield it as it is found?
    /// </summary>
    /// <remarks>
    /// <para>
    /// True for what is gathered, felled, quarried or hunted as itself - timber, raw rock, bone, hides, the plant matter a tile grows. Otherwise False.
    /// </para>
    /// </remarks>
    public bool IsNaturallyOccurring { get; set; }

    /// <summary>
    /// Where the quantity behind this Asset is held - a settlement's bank, the Player, or nowhere at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to <see cref="AssetStorageScope.SettlementBank"/>, which is nearly everything. The exceptions are the experience points, held per Player, and
    /// the population figures, which are derived at read time from the settlements and rosters that already record them.
    /// </para>
    /// <para>
    /// This keeps the catalogue row a pure label: it says what the Asset <i>is</i>, and this column says where to go for its number. Only a
    /// <see cref="AssetStorageScope.SettlementBank"/> Asset ever has an <see cref="AssetBankBalance"/>.
    /// </para>
    /// <para>
    /// Not to be confused with <see cref="StorageClassId"/>. This says where the number comes from; that says how the thing is kept once it is in a bank.
    /// </para>
    /// </remarks>
    public AssetStorageScope StorageScope { get; set; } = AssetStorageScope.SettlementBank;

    /// <summary>
    /// The storage class this Asset is kept under, which is what a capacity cap binds against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exactly one, and required. Nullable only until the migration that fills it on existing rows has run, after which the column itself is what enforces
    /// the guarantee - which is the whole reason this is a foreign key rather than a tag.
    /// </para>
    /// <para>
    /// Every Asset carries one, including those no bank ever holds: the experience points and the population figures are classed because the column requires
    /// it, not because anything will ever grant them capacity.
    /// </para>
    /// </remarks>
    public int? StorageClassId { get; set; }

    public StorageClass? StorageClass { get; set; }

    public AssetMetaCategory MetaCategory { get; set; } = null!;

    /// <summary>
    /// The per-bank balances of this Asset, across every Player's banks.
    /// </summary>
    /// <remarks>Empty for an Asset that is not held in a bank, and the inverse the balance configuration's foreign key is declared against.</remarks>
    public ICollection<AssetBankBalance> BankBalances { get; set; } = [];
}
