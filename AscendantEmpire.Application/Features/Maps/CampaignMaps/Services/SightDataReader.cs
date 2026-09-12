using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;

using System.Linq.Expressions;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <inheritdoc cref="ISightDataReader" />
internal class SightDataReader : ISightDataReader
{
    /// <summary>
    /// A world-map tile as the sight rules see it. Everything is read from the world map's own Biome and Geography, never a Player's copy.
    /// </summary>
    private static readonly Expression<Func<CampaignMap, SightTerrain>> _TERRAIN_PROJECTION = tile => new SightTerrain
    {
        CampaignMapId = tile.CampaignMapId,
        X = tile.XCoord,
        Y = tile.YCoord,
        BiomeType = tile.BiomeTypeId,
        GeographyType = tile.CampaignMapGeographyTypeId,
        DoesBlockSight = tile.Biome.DoesBlockSight,
        RelativeElevation = tile.Geography.RelativeElevation,
        GeographyName = tile.Geography.Name,
        GeographyMovementCost = tile.Geography.MovementCost,
        BiomeName = tile.Biome.Name,
        BiomeMovementCost = tile.Biome.MovementCost,
        BiomeIsWater = tile.Biome.IsWater,
        BiomeIsTraversable = tile.Biome.IsTraversable
    };

    /// <summary>
    /// A standing Unit as an Observer. Its Sight is the compiled template's, divided down to whole tiles inline because this runs as SQL.
    /// </summary>
    /// <remarks>
    /// Only scalars are projected off <c>CompiledUnitTemplate</c>, never its damage profiles, which is what keeps this readable on the in-memory tier.
    /// </remarks>
    private static readonly Expression<Func<UnitInstance, SightObserver>> _UNIT_OBSERVER_PROJECTION = force => new SightObserver
    {
        Id = force.Id,
        RecordType = RecordType.Unit,
        PlayerId = force.PlayerId,
        Location = new Coord(force.CampaignMap.XCoord, force.CampaignMap.YCoord),
        BaseSightRange = (int)(force.UnitTemplate.Compiled!.TotalSight / ScaledAmount.Scale),
        MovementDomain = force.UnitTemplate.Compiled!.MovementDomain,
        Detection = force.UnitTemplate.Compiled!.TotalDetection,
        HealthyTroops = force.HealthyCount,
        UnitTemplateId = force.UnitTemplateId
    };

    /// <summary>
    /// A standing campaign-map Structure as an Observer.
    /// </summary>
    /// <remarks>
    /// A Structure cannot move, so it is judged as a land Force when dense terrain blinds it, and it has no Detection stat of its own yet.
    /// </remarks>
    private static readonly Expression<Func<StructureInstance, SightObserver>> _STRUCTURE_OBSERVER_PROJECTION = structure => new SightObserver
    {
        Id = structure.Id,
        RecordType = RecordType.Structure,
        PlayerId = structure.OwnedByPlayerId,
        Location = new Coord(structure.CampaignMap.XCoord, structure.CampaignMap.YCoord),
        BaseSightRange = structure.Structure.SightRange,
        MovementDomain = DomainType.Land,
        Detection = SightingFidelityRules.STRUCTURE_DETECTION
    };

    /// <summary>
    /// A Structure a Player lost during a Turn - razed, or captured from them - reduced to what is needed to rebuild it as that Player's Observer until the moment it was lost.
    /// </summary>
    /// <param name="OwnerBeforePlayerId">Who held the Structure before the loss, and so whose Observer it was.</param>
    /// <param name="LostAtSequence">The sequence of the first capture or razing, which becomes <see cref="SightObserver.DestroyedAtSequence"/>.</param>
    private sealed record LostStructure(int StructureInstanceId, int OwnerBeforePlayerId, int LostAtSequence, Coord Location, int SightRange);

    /// <inheritdoc />
    public async Task<CampaignMapGeometry?> GetGeometryAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Positional rather than named arguments: an expression tree cannot carry named argument specifications. The order is offset, width, height, then
        // horizontal and vertical wrapping.
        return await context.GameIndex
            .AsNoTracking()
            .Where(game => game.Id == gameId)
            .Select(game => new CampaignMapGeometry(game.CampaignMapOffsetId, game.MapWidth, game.MapHeight, game.HorizontalWrap, game.VerticalWrap))
            .SingleOrDefaultAsync(cancelToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<(int X, int Y), SightTerrain>> GetTerrainAsync(
        AscendantEmpireContext context,
        int gameId,
        IEnumerable<int> campaignMapIds,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(campaignMapIds);

        int[] tileIds = [.. campaignMapIds.Distinct()];

        if (tileIds.Length == 0)
        {
            return new Dictionary<(int X, int Y), SightTerrain>();
        }

        List<SightTerrain> tiles = await context.CampaignMaps
            .AsNoTracking()
            .Where(tile => tile.GameId == gameId && tileIds.Contains(tile.CampaignMapId))
            .Select(_TERRAIN_PROJECTION)
            .ToListAsync(cancelToken);

        return tiles.ToDictionary(tile => (tile.X, tile.Y));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SightObserver>> GetPlayerObserversAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Structures first and Units second, which is the order the map read has always reported its Observers in.
        List<SightObserver> structures = await context.StructureInstances
            .AsNoTracking()
            .Where(structure => structure.GameId == gameId
                && structure.OwnedByPlayerId == playerId
                && structure.BattleMapTileId == null // campaign map only
                && structure.RemovedOnTurnId == null) // still standing
            .Select(_STRUCTURE_OBSERVER_PROJECTION)
            .ToListAsync(cancelToken);

        List<SightObserver> units = await context.UnitInstances
            .AsNoTracking()
            .Where(force => force.GameId == gameId && force.PlayerId == playerId)
            .Select(_UNIT_OBSERVER_PROJECTION)
            .ToListAsync(cancelToken);

        return [.. structures, .. units];
    }

    /// <inheritdoc />
    public async Task<int> GetMaxSightReachAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> observingPlayerIds,
        int playerTurnId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observingPlayerIds);

        int[] playerIds = [.. observingPlayerIds.Distinct()];

        if (playerIds.Length == 0)
        {
            return 0;
        }

        // Each maximum is taken over a nullable projection so that "no rows at all" comes back as null rather than throwing.
        long? maxUnitSight = await context.UnitInstances
            .AsNoTracking()
            .Where(force => force.GameId == gameId && playerIds.Contains(force.PlayerId))
            .Select(force => (long?)force.UnitTemplate.Compiled!.TotalSight)
            .MaxAsync(cancelToken);

        int? maxStructureSight = await context.StructureInstances
            .AsNoTracking()
            .Where(structure => structure.GameId == gameId
                && playerIds.Contains(structure.OwnedByPlayerId)
                && structure.BattleMapTileId == null
                && structure.RemovedOnTurnId == null)
            .Select(structure => (int?)structure.Structure.SightRange)
            .MaxAsync(cancelToken);

        // Units destroyed this Turn have no UnitInstances row, so their Sight comes through the template their history row recorded.
        int[] destroyedTemplateIds = await DestroyedThisTurn(context, gameId, playerIds, playerTurnId)
            .Select(row => row.UnitTemplateId)
            .Distinct()
            .ToArrayAsync(cancelToken);

        long? maxDestroyedUnitSight = null;

        if (destroyedTemplateIds.Length > 0)
        {
            maxDestroyedUnitSight = await context.CompiledUnitTemplates
                .AsNoTracking()
                .Where(compiled => destroyedTemplateIds.Contains(compiled.UnitTemplateId))
                .Select(compiled => (long?)compiled.TotalSight)
                .MaxAsync(cancelToken);
        }

        // Structures these Players lost this Turn watched until they were lost. A razed one no longer stands and a captured one belongs to someone else now, so neither is in the standing
        // maximum above, and each has to be counted here or an Observer that saw something could fall outside the box.
        IReadOnlyCollection<LostStructure> lostStructures = await GetStructuresLostThisTurnAsync(context, gameId, playerIds, playerTurnId, tileIds: null, cancelToken);

        int maxLostStructureSight = lostStructures.Count == 0 ? 0 : lostStructures.Max(structure => structure.SightRange);

        // High ground adds its elevation to an Observer's range. Only Geographies a Force can enter count: nothing stands on a Mountain to take the bonus.
        int? maxEnterableElevation = await context.CampaignGeographies
            .AsNoTracking()
            .Where(geography => geography.MovementCost < CampaignGeography.IMPASSABLE_MOVEMENT_COST)
            .Select(geography => (int?)geography.RelativeElevation)
            .MaxAsync(cancelToken);

        int maxBaseSight = new[] { ToWholeTiles(maxUnitSight), maxStructureSight ?? 0, ToWholeTiles(maxDestroyedUnitSight), maxLostStructureSight }.Max();

        // Sea-level and lower ground is never a penalty, so a negative maximum adds nothing rather than shrinking the box.
        return maxBaseSight + Math.Max(0, maxEnterableElevation ?? 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SightObserver>> GetObserversOnTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> observingPlayerIds,
        int playerTurnId,
        IEnumerable<int> campaignMapIds,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observingPlayerIds);
        ArgumentNullException.ThrowIfNull(campaignMapIds);

        int[] playerIds = [.. observingPlayerIds.Distinct()];
        int[] tileIds = [.. campaignMapIds.Distinct()];

        if (playerIds.Length == 0 || tileIds.Length == 0)
        {
            return [];
        }

        List<SightObserver> standingStructures = await context.StructureInstances
            .AsNoTracking()
            .Where(structure => structure.GameId == gameId
                && playerIds.Contains(structure.OwnedByPlayerId)
                && structure.BattleMapTileId == null
                && structure.RemovedOnTurnId == null
                && tileIds.Contains(structure.CampaignMapTileId))
            .Select(_STRUCTURE_OBSERVER_PROJECTION)
            .ToListAsync(cancelToken);

        List<SightObserver> standingUnits = await context.UnitInstances
            .AsNoTracking()
            .Where(force => force.GameId == gameId && playerIds.Contains(force.PlayerId) && tileIds.Contains(force.CampaignMapTileId))
            .Select(_UNIT_OBSERVER_PROJECTION)
            .ToListAsync(cancelToken);

        IReadOnlyCollection<SightObserver> destroyedUnits = await GetUnitsDestroyedOnTilesAsync(context, gameId, playerIds, playerTurnId, tileIds, cancelToken);

        IReadOnlyCollection<LostStructure> lostStructures = await GetStructuresLostThisTurnAsync(context, gameId, playerIds, playerTurnId, tileIds, cancelToken);

        // A lost Structure watches for the Player who held it before the loss, from where it stands, up to - and not including - the sequence it was lost at.
        IEnumerable<SightObserver> lostStructureObservers = lostStructures.Select(structure => new SightObserver
        {
            Id = structure.StructureInstanceId,
            RecordType = RecordType.Structure,
            PlayerId = structure.OwnerBeforePlayerId,
            Location = structure.Location,
            BaseSightRange = structure.SightRange,
            MovementDomain = DomainType.Land,
            Detection = SightingFidelityRules.STRUCTURE_DETECTION,
            DestroyedAtSequence = structure.LostAtSequence
        });

        List<SightObserver> lostObservers = [.. destroyedUnits, .. lostStructureObservers];

        // Where an Observer somehow has both a standing record and a lost one, the lost one wins, so it is never counted for sequences after the loss. Keyed on the record type as well as the
        // id: a Unit and a Structure are numbered independently, and a bare id would let one knock the other out.
        HashSet<(RecordType RecordType, int Id)> lostObserverKeys = [.. lostObservers.Select(observer => (observer.RecordType, observer.Id))];

        return
        [
            .. standingStructures.Where(observer => lostObserverKeys.Contains((observer.RecordType, observer.Id)) == false),
            .. standingUnits.Where(observer => lostObserverKeys.Contains((observer.RecordType, observer.Id)) == false),
            .. lostObservers
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, long>> GetTemplateVisibilityAsync(AscendantEmpireContext context, IEnumerable<int> unitTemplateIds, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unitTemplateIds);

        int[] templateIds = [.. unitTemplateIds.Distinct()];

        if (templateIds.Length == 0)
        {
            return new Dictionary<int, long>();
        }

        // Projected to the two scalars before the dictionary is built. ToDictionaryAsync's selectors run on the client, so without the Select the whole
        // compiled template - damage profiles and all - would be materialised just to read one column of it.
        return await context.CompiledUnitTemplates
            .AsNoTracking()
            .Where(compiled => templateIds.Contains(compiled.UnitTemplateId))
            .Select(compiled => new { compiled.UnitTemplateId, compiled.TotalVisibility })
            .ToDictionaryAsync(compiled => compiled.UnitTemplateId, compiled => compiled.TotalVisibility, cancelToken);
    }

    /// <summary>
    /// Rebuilds the Units destroyed on the given tiles during a Turn as Observers, from their history rows.
    /// </summary>
    /// <remarks>
    /// Two queries rather than one join: the history rows with their tile coordinates first, then only the scalar Sight stats of the templates they name.
    /// Joining <c>CompiledUnitTemplate</c> straight into the row query would put an entity that owns complex types into the shape, which the in-memory
    /// provider cannot run.
    /// </remarks>
    private static async Task<IReadOnlyCollection<SightObserver>> GetUnitsDestroyedOnTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        int[] playerIds,
        int playerTurnId,
        int[] tileIds,
        CancellationToken cancelToken)
    {
        var destroyedRows = await (from row in DestroyedThisTurn(context, gameId, playerIds, playerTurnId)
                                   join tile in context.CampaignMaps.AsNoTracking()
                                       on row.CampaignMapTileId equals tile.CampaignMapId
                                   where tileIds.Contains(row.CampaignMapTileId)
                                   select new
                                   {
                                       row.UnitInstanceId,
                                       row.PlayerId,
                                       row.UnitTemplateId,
                                       row.TurnSequence,
                                       tile.XCoord,
                                       tile.YCoord
                                   }).ToListAsync(cancelToken);

        if (destroyedRows.Count == 0)
        {
            return [];
        }

        int[] templateIds = [.. destroyedRows.Select(row => row.UnitTemplateId).Distinct()];

        var templateStats = await context.CompiledUnitTemplates
            .AsNoTracking()
            .Where(compiled => templateIds.Contains(compiled.UnitTemplateId))
            .Select(compiled => new
            {
                compiled.UnitTemplateId,
                compiled.TotalSight,
                compiled.TotalDetection,
                compiled.MovementDomain
            })
            .ToDictionaryAsync(stats => stats.UnitTemplateId, cancelToken);

        List<SightObserver> observers = [];

        foreach (var row in destroyedRows)
        {
            // A template with no compiled snapshot has no Sight to reconstruct. Such a Unit could never have been seeing anything while it stood either.
            if (templateStats.TryGetValue(row.UnitTemplateId, out var stats) == false)
            {
                continue;
            }

            observers.Add(new SightObserver
            {
                Id = row.UnitInstanceId,
                RecordType = RecordType.Unit,
                PlayerId = row.PlayerId,
                Location = new Coord(row.XCoord, row.YCoord),
                BaseSightRange = ToWholeTiles(stats.TotalSight),
                MovementDomain = stats.MovementDomain,
                Detection = stats.TotalDetection,
                HealthyTroops = null,
                UnitTemplateId = row.UnitTemplateId,
                DestroyedAtSequence = row.TurnSequence
            });
        }

        return observers;
    }

    /// <summary>
    /// The Structures the given Players lost during one Turn - razed, or captured from them - with the sequence each was first lost at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A capture counts as a loss exactly as a razing does. It flips the Structure's owner to the acting Player, who is never an observing Player, so without this the Structure would stop
    /// witnessing everything its real owner's tower saw before it was taken - the same hole razing would leave.
    /// </para>
    /// <para>
    /// <b>Only the first loss matters.</b> During a Turn only the acting Player acts, so whoever held the Structure after its first capture was the acting Player, and is excluded anyway. The
    /// owner before that first loss is read off the row: the previous owner of a <c>Captured</c> row, or the owner of a <c>Razed</c> one.
    /// </para>
    /// <para>
    /// The history rows are filtered in the query and the first loss picked and filtered by owner in memory, since the owner before the loss lives in a different column for each event type.
    /// A Turn's losses are a handful of rows at most.
    /// </para>
    /// </remarks>
    /// <param name="tileIds">The tiles to search, or <see langword="null"/> for the whole map.</param>
    private static async Task<IReadOnlyCollection<LostStructure>> GetStructuresLostThisTurnAsync(
        AscendantEmpireContext context,
        int gameId,
        int[] playerIds,
        int playerTurnId,
        int[]? tileIds,
        CancellationToken cancelToken)
    {
        IQueryable<StructureHistory> losses = context.StructureHistory
            .AsNoTracking()
            .Where(row => row.GameId == gameId
                && row.PlayerTurnId == playerTurnId
                && (row.EventType == StructureHistoryEventType.Captured || row.EventType == StructureHistoryEventType.Razed));

        if (tileIds is not null)
        {
            losses = losses.Where(row => tileIds.Contains(row.CampaignMapTileId));
        }

        var lossRows = await (from row in losses
                              join structure in context.StructureInstances.AsNoTracking()
                                  on row.StructureInstanceId equals structure.Id
                              select new
                              {
                                  row.Id,
                                  row.StructureInstanceId,
                                  row.EventType,
                                  row.PlayerId,
                                  row.PreviousOwnerPlayerId,
                                  row.TurnSequence,
                                  structure.CampaignMap.XCoord,
                                  structure.CampaignMap.YCoord,
                                  structure.Structure.SightRange
                              }).ToListAsync(cancelToken);

        return
        [
            .. lossRows
                .GroupBy(row => row.StructureInstanceId)
                .Select(structureRows => structureRows.OrderBy(row => row.TurnSequence).ThenBy(row => row.Id).First())
                .Select(firstLoss => new LostStructure(
                    firstLoss.StructureInstanceId,
                    // A Captured row always carries its previous owner - a check constraint makes sure of it - so the fallback is never taken in practice.
                    OwnerBeforePlayerId: firstLoss.EventType == StructureHistoryEventType.Captured ? firstLoss.PreviousOwnerPlayerId ?? firstLoss.PlayerId : firstLoss.PlayerId,
                    LostAtSequence: firstLoss.TurnSequence,
                    Location: new Coord(firstLoss.XCoord, firstLoss.YCoord),
                    SightRange: firstLoss.SightRange))
                .Where(structure => playerIds.Contains(structure.OwnerBeforePlayerId))
        ];
    }

    /// <summary>
    /// The <c>Destroyed</c> history rows of the given Players' Units during one Turn.
    /// </summary>
    private static IQueryable<MovementHistory> DestroyedThisTurn(AscendantEmpireContext context, int gameId, int[] playerIds, int playerTurnId)
    {
        return context.MovementHistory
            .AsNoTracking()
            .Where(row => row.GameId == gameId
                && row.PlayerTurnId == playerTurnId
                && row.EventType == ForceHistoryEventType.Destroyed
                && playerIds.Contains(row.PlayerId));
    }

    /// <summary>
    /// Converts a fixed-point Sight at Scale 1,000 into whole tiles, the same truncating division the Unit projection makes in SQL.
    /// </summary>
    private static int ToWholeTiles(long? scaledSight)
    {
        return (int)((scaledSight ?? 0) / ScaledAmount.Scale);
    }
}
