using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Every database read the field-of-view rules depend on, turned into the flat snapshots <see cref="FieldOfView"/> works over.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the pure sight code and the database. Keeping every query here, and nothing else, is what lets <see cref="IFieldOfViewService"/> and
/// <see cref="IWitnessService"/> be exercised against a substitute, and the queries themselves on the in-memory tier.
/// </para>
/// <para>
/// No raw SQL, deliberately. Id lists go through a plain <c>Contains</c>, which EF sends to SQL Server as a single <c>OPENJSON</c> parameter, so the
/// query stays one round-trip however many tiles it carries — and the in-memory provider can run it.
/// </para>
/// <para>
/// Every method reads through the caller's context, so a read made during the end-of-turn pipeline sees that pipeline's uncommitted writes.
/// </para>
/// </remarks>
internal interface ISightDataReader
{
    /// <summary>
    /// The shape of a Game's campaign map, or null when there is no such Game.
    /// </summary>
    Task<CampaignMapGeometry?> GetGeometryAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);

    /// <summary>
    /// World-map terrain for the given tiles, keyed by coordinate.
    /// </summary>
    /// <remarks>
    /// Ids that belong to no tile of this Game are simply absent from the result.
    /// </remarks>
    /// <param name="campaignMapIds">World-map tile ids. Duplicates are harmless.</param>
    Task<IReadOnlyDictionary<(int X, int Y), SightTerrain>> GetTerrainAsync(AscendantEmpireContext context, int gameId, IEnumerable<int> campaignMapIds, CancellationToken cancelToken = default);

    /// <summary>
    /// Every Observer one Player owns: their campaign-map Structures that still stand, then their Units.
    /// </summary>
    /// <remarks>
    /// The live map read's Observers. Units are not filtered on troops or on being in a Battle, matching what the map read has always counted as an
    /// Observer.
    /// </remarks>
    Task<IReadOnlyCollection<SightObserver>> GetPlayerObserversAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancelToken = default);

    /// <summary>
    /// The furthest any of the given Players' Observers could possibly see during a Turn, in tiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The radius of the box the witness service searches for Observers around a probed tile. It has to be an upper bound — an Observer outside the box
    /// is never considered, however far it can see — but the tighter it is, the less is loaded.
    /// </para>
    /// <para>
    /// The largest base Sight among the Players' Units, standing Structures and Units destroyed during <paramref name="playerTurnId"/>, plus the
    /// greatest elevation bonus on any Geography that can be <b>entered</b>. Nothing stands on a Mountain, and counting the Void's elevation of ten would
    /// balloon every box for ground no Observer can occupy.
    /// </para>
    /// </remarks>
    /// <param name="playerTurnId">The Turn whose destroyed Units still count.</param>
    Task<int> GetMaxSightReachAsync(AscendantEmpireContext context, int gameId, IReadOnlyCollection<int> observingPlayerIds, int playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// The given Players' Observers standing on the given tiles, including Units that were destroyed there during <paramref name="playerTurnId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Unit destroyed during the Turn has no row left in <c>UnitInstances</c>, so it is rebuilt from its <c>MovementHistory</c> <c>Destroyed</c> row: the
    /// row's owner, template (for its Sight, domain and Detection), tile and sequence, which becomes <see cref="SightObserver.DestroyedAtSequence"/>. Its
    /// troops are no longer known.
    /// </para>
    /// <para>
    /// Where a Unit somehow has both, the destroyed record wins, so an Observer is never counted for sequences after it died.
    /// </para>
    /// </remarks>
    /// <param name="campaignMapIds">World-map tile ids of the box to search.</param>
    Task<IReadOnlyCollection<SightObserver>> GetObserversOnTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> observingPlayerIds,
        int playerTurnId,
        IEnumerable<int> campaignMapIds,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Each template's compiled <c>TotalVisibility</c>, keyed by template id. Templates with no compiled snapshot are absent.
    /// </summary>
    Task<IReadOnlyDictionary<int, long>> GetTemplateVisibilityAsync(AscendantEmpireContext context, IEnumerable<int> unitTemplateIds, CancellationToken cancelToken = default);
}
