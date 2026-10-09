namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// A MapResource on one campaign map tile in a game: an instance of a <c>MapResourceType</c>, such as the berry patch on tile (4,5).
/// </summary>
/// <remarks>
/// <para>
/// Player-neutral, like <see cref="CampaignMapDepletion"/>: every Player extracts from the same quantity. What a Player knows of it is in
/// <see cref="PlayerKnownMapResource"/>, and what they are shown is an estimate of the quantity, never the quantity itself. See
/// <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// Where its quantity lives depends on its type's kind:
/// <list type="bullet">
///   <item><description>a single-quantity type (renewable or not) uses <see cref="Quantity"/>;</description></item>
///   <item><description>a Distribution uses its <see cref="MapResourceInstanceAmount"/> rows, one per Asset, and leaves <see cref="Quantity"/> null;</description></item>
///   <item><description>a stratified type uses its <see cref="MapResourceInstanceLayer"/> rows, and leaves <see cref="Quantity"/> null.</description></item>
/// </list>
/// </para>
/// <para>
/// Every roll concerning an instance (spread, survival, capture, discovery, a Player's estimation error) is derived from the game's seed and the instance's <see cref="Id"/>,
/// so no roll's result is stored. A roll taken again for the same instance — survival, spread, discovery, capture — also keys on the Round, since without it every Round
/// would draw the same number. A roll taken once keys on the instance alone: the estimation error in particular is recomputed on every view, which keeps it fixed without a
/// column that could drift. See the Randomness section of the design doc.
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="CampaignMapDepletion"/>: the rows are read and written by tile and game, never walked as an entity graph.
/// </para>
/// </remarks>
public class MapResourceInstance
{
    /// <summary>
    /// The lowest slot on a tile.
    /// </summary>
    public const byte MIN_SLOT = 1;

    /// <summary>
    /// The highest slot on a tile, and so the most MapResources a tile can hold.
    /// </summary>
    public const byte MAX_SLOT = 2;

    /// <summary>
    /// Identity. Part of the key of every roll concerning the instance, so it must never be reassigned.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The Game the tile belongs to.
    /// </summary>
    /// <remarks>
    /// Implied by <see cref="CampaignMapId"/>, but carried so the end-of-Round process can load a game's instances, and the game delete can purge them, directly.
    /// </remarks>
    public int GameId { get; set; }

    /// <summary>
    /// The campaign map tile the instance is on.
    /// </summary>
    public int CampaignMapId { get; set; }

    /// <summary>
    /// What kind of MapResource it is (a <c>map.MapResourceType</c>).
    /// </summary>
    public int MapResourceTypeId { get; set; }

    /// <summary>
    /// Which of the tile's places it takes: <see cref="MIN_SLOT"/> or <see cref="MAX_SLOT"/>.
    /// </summary>
    /// <remarks>
    /// A dormant instance keeps its slot, and so still blocks other MapResources spreading onto the tile.
    /// </remarks>
    public byte Slot { get; set; }

    /// <summary>
    /// The quantity on the tile, stored scaled by <c>ScaledAmount.Scale</c> (1,000), or <see langword="null"/> for a Distribution or a stratified instance, which hold theirs
    /// in child rows.
    /// </summary>
    /// <remarks>
    /// Never negative. 0 means exhausted: a renewable instance at 0 is dormant (see <see cref="DormantSinceRound"/>).
    /// </remarks>
    public long? Quantity { get; set; }

    /// <summary>
    /// How many more spreads the instance can make, or <see langword="null"/> for a type that never spreads.
    /// </summary>
    /// <remarks>
    /// Each spread that uses one up decrements it, and a new or revived instance takes the value after the decrement, so each generation can spread less than the one before.
    /// </remarks>
    public int? SpreadLimit { get; set; }

    /// <summary>
    /// The Round a renewable instance became exhausted, or <see langword="null"/> while it is active.
    /// </summary>
    /// <remarks>
    /// A round stamp rather than a counter, as with <see cref="CampaignMapDepletion.FedOnRound"/>, so nothing needs updating on the Rounds it merely survives. It is deleted
    /// once it has been dormant for its type's <c>SurvivalRounds</c> without reseeding.
    /// </remarks>
    public int? DormantSinceRound { get; set; }

    /// <summary>
    /// How far off the quantity shown to Players can be, as a fraction, rolled when the instance was created. <see langword="null"/> for a stratified instance, whose layers
    /// each have their own.
    /// </summary>
    /// <remarks>
    /// A Player's modifiers lower it for that Player only.
    /// </remarks>
    public decimal? EstimationFuzzyFactor { get; set; }

    /// <summary>
    /// The Distribution scenario the instance was created from, or <see langword="null"/> for any other kind.
    /// </summary>
    /// <remarks>
    /// For reference only: the amounts were copied into <see cref="MapResourceInstanceAmount"/> rows when the instance was created, and are never read back from the scenario.
    /// </remarks>
    public int? MapResourceDistributionScenarioId { get; set; }

    /// <summary>
    /// The Round the instance was created in: 0 at game setup, or the Round it spread in.
    /// </summary>
    public int CreatedOnRound { get; set; }
}
