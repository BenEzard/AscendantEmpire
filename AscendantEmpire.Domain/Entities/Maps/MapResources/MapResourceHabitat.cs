using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// Where a MapResourceType can exist, a Biome and optionally one Geography in it, and the type's values on tiles there.
/// </summary>
/// <remarks>
/// <para>
/// No habitat means non-compatible: the type cannot exist on the tile, and cannot spread onto it. Berries have no Desert habitat, so they never grow there.
/// </para>
/// <para>
/// A <see langword="null"/> Geography covers every Geography in the Biome. When a tile matches both a Geography-specific habitat and the Biome-wide one, the specific one wins.
/// </para>
/// <para>
/// These are the values on unclaimed tiles. On a claimed tile the claimant's <c>PlayerMapResourceModifier</c> rows raise them. Which values must be set depends on the type's
/// kind, and is checked by <c>MapResourceTypeValidator</c>.
/// </para>
/// </remarks>
public class MapResourceHabitat
{
    /// <summary>
    /// Surrogate key.
    /// </summary>
    /// <remarks>
    /// The natural key includes <see cref="CampaignMapGeographyTypeId"/>, which is nullable and so cannot be part of a primary key. Uniqueness is enforced by the
    /// <c>UX_MapResourceHabitat_Type_Biome_Geography</c> index instead.
    /// </remarks>
    public int Id { get; set; }

    public int MapResourceTypeId { get; set; }

    public MapResourceType MapResourceType { get; set; } = null!;

    public BiomeType BiomeTypeId { get; set; }

    /// <summary>
    /// The Geography this habitat applies to, or <see langword="null"/> for every Geography in the Biome.
    /// </summary>
    public CampaignMapGeographyType? CampaignMapGeographyTypeId { get; set; }

    /// <summary>
    /// The lowest quantity an instance created at game setup is rolled with, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    /// <remarks>
    /// Used only when the map editor did not set the instance's quantity itself. <see langword="null"/> for a Distribution (which starts at its scenario's amounts) and a
    /// stratified type (whose layers have their own ranges).
    /// </remarks>
    public long? InitialQuantityMin { get; set; }

    /// <summary>
    /// The highest quantity an instance created at game setup is rolled with. See <see cref="InitialQuantityMin"/>.
    /// </summary>
    public long? InitialQuantityMax { get; set; }

    /// <summary>
    /// The ceiling a renewable instance grows to, stored scaled by <c>ScaledAmount.Scale</c> (1,000). For a Distribution it caps the total of all its Assets.
    /// <see langword="null"/> for a type that is not renewable.
    /// </summary>
    public long? MaximumQuantity { get; set; }

    /// <summary>
    /// The share of its current quantity a renewable instance grows each Round, as a fraction (0.1 is 10%). <see langword="null"/> for a type that is not renewable.
    /// </summary>
    public decimal? GrowthPercentage { get; set; }

    /// <summary>
    /// The chance, as a fraction, each Round that an instance at its ceiling spreads to a neighbouring tile. <see langword="null"/> for a type that never spreads.
    /// </summary>
    public decimal? SpreadLikelihood { get; set; }

    /// <summary>
    /// The lowest <c>SpreadLimit</c> an instance created at game setup is rolled with. <see langword="null"/> for a type that never spreads.
    /// </summary>
    public int? SpreadLimitMin { get; set; }

    /// <summary>
    /// The highest <c>SpreadLimit</c> an instance created at game setup is rolled with. <see langword="null"/> for a type that never spreads.
    /// </summary>
    public int? SpreadLimitMax { get; set; }
}
