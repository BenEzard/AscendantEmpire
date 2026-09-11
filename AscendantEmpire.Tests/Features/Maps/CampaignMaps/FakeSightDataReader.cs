using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// An <see cref="ISightDataReader"/> over an in-memory grid and a list of Observers, which records what it was asked for.
/// </summary>
/// <remarks>
/// <para>
/// A hand-written fake rather than a substitute, because the services under test ask for terrain by id set and in two passes, and answering that
/// faithfully from a grid is simpler than arranging a substitute call by call.
/// </para>
/// <para>
/// The context it is handed is never touched: every answer comes from the grid and the lists here.
/// </para>
/// </remarks>
internal sealed class FakeSightDataReader(SightTerrainGrid grid) : ISightDataReader
{
    /// <summary>The Game's geometry, or null to act as though the Game does not exist.</summary>
    public CampaignMapGeometry? Geometry { get; set; } = grid.Geometry;

    /// <summary>What <see cref="GetMaxSightReachAsync"/> answers.</summary>
    public int SightReach { get; set; }

    /// <summary>Every Observer "in the database", standing or destroyed, for any Player.</summary>
    public List<SightObserver> Observers { get; } = [];

    public Dictionary<int, long> VisibilityByTemplateId { get; } = [];

    /// <summary>Each batch of tile ids terrain was asked for, in the order asked.</summary>
    public List<IReadOnlyCollection<int>> TerrainRequests { get; } = [];

    public IReadOnlyCollection<int>? RequestedObservingPlayerIds { get; private set; }

    public IReadOnlyCollection<int>? RequestedObserverTileIds { get; private set; }

    /// <summary>How many reads of any kind were made.</summary>
    public int ReadCount { get; private set; }

    public Task<CampaignMapGeometry?> GetGeometryAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        ReadCount++;
        return Task.FromResult(Geometry);
    }

    public Task<IReadOnlyDictionary<(int X, int Y), SightTerrain>> GetTerrainAsync(AscendantEmpireContext context, int gameId, IEnumerable<int> campaignMapIds, CancellationToken cancelToken = default)
    {
        ReadCount++;

        HashSet<int> tileIds = [.. campaignMapIds];
        TerrainRequests.Add(tileIds);

        IReadOnlyDictionary<(int X, int Y), SightTerrain> terrain = grid.Terrain.Where(entry => tileIds.Contains(entry.Value.CampaignMapId)).ToDictionary();
        return Task.FromResult(terrain);
    }

    public Task<IReadOnlyCollection<SightObserver>> GetPlayerObserversAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancelToken = default)
    {
        ReadCount++;

        IReadOnlyCollection<SightObserver> observers = [.. Observers.Where(observer => observer.PlayerId == playerId)];
        return Task.FromResult(observers);
    }

    public Task<int> GetMaxSightReachAsync(AscendantEmpireContext context, int gameId, IReadOnlyCollection<int> observingPlayerIds, int playerTurnId, CancellationToken cancelToken = default)
    {
        ReadCount++;
        return Task.FromResult(SightReach);
    }

    public Task<IReadOnlyCollection<SightObserver>> GetObserversOnTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> observingPlayerIds,
        int playerTurnId,
        IEnumerable<int> campaignMapIds,
        CancellationToken cancelToken = default)
    {
        ReadCount++;

        RequestedObservingPlayerIds = [.. observingPlayerIds];
        RequestedObserverTileIds = [.. campaignMapIds];

        HashSet<int> tileIds = [.. RequestedObserverTileIds];

        IReadOnlyCollection<SightObserver> observers =
        [
            .. Observers.Where(observer => observingPlayerIds.Contains(observer.PlayerId) && tileIds.Contains(grid.IdOf(observer.Location.X, observer.Location.Y)))
        ];

        return Task.FromResult(observers);
    }

    public Task<IReadOnlyDictionary<int, long>> GetTemplateVisibilityAsync(AscendantEmpireContext context, IEnumerable<int> unitTemplateIds, CancellationToken cancelToken = default)
    {
        ReadCount++;

        HashSet<int> templateIds = [.. unitTemplateIds];

        IReadOnlyDictionary<int, long> visibility = VisibilityByTemplateId.Where(entry => templateIds.Contains(entry.Key)).ToDictionary();
        return Task.FromResult(visibility);
    }
}
