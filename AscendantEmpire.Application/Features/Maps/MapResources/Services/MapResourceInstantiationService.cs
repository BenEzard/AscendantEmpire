using System.Data;

using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <inheritdoc cref="IMapResourceInstantiationService"/>
internal sealed class MapResourceInstantiationService(
    ILogger<MapResourceInstantiationService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IMapResourceInstantiationService
{
    /// <summary>
    /// How many rows are sent to the server at a time.
    /// </summary>
    private const int _BULK_INSERT_BATCH_SIZE = 10_000;

    /// <summary>
    /// The Round every instance game setup creates is stamped with, which is what tells an original instance from one that spread later.
    /// </summary>
    private const int _CREATED_ON_ROUND = 0;

    public async Task<ServiceResult<int>> InstantiateForGameAsync(
        int gameId,
        int campaignMapGalleryIndexId,
        long gameSeed,
        int campaignMapOffsetId,
        int mapWidth,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            List<PlacementToInstantiate> placements = await ReadPlacementsAsync(context, campaignMapGalleryIndexId, cancellationToken);

            // A map need not carry MapResources, so no placements is a success with nothing to do rather than a shortfall.
            if (placements.Count == 0)
            {
                return ServiceResult<int>.Succeed(0);
            }

            List<int> typeIds = [.. placements.Select(placement => placement.MapResourceTypeId).Distinct()];

            Dictionary<int, MapResourceTypeValues> typesById = await ReadTypesAsync(context, typeIds, cancellationToken);

            MapResourceInstantiationPlan plan = MapResourceInstantiationRules.Plan(
                gameSeed,
                campaignMapOffsetId,
                mapWidth,
                placements,
                typesById);

            LogRefusals(plan.Refusals, gameId);

            if (plan.ToCreate.Count == 0)
            {
                return ServiceResult<int>.Succeed(0);
            }

            string connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The context has no connection string, so the MapResource instances cannot be bulk-inserted.");

            await BulkInsertAsync(
                connectionString,
                context.SchemaQualifiedTableName<MapResourceInstance>(),
                BuildInstanceTable(gameId, plan.ToCreate),
                cancellationToken);

            // The parent ids are assigned by the database, so the children can only be attached once the rows exist. Filtered by Game: a database holding
            // several Games would otherwise hand back rows these children do not belong to.
            Dictionary<(int CampaignMapId, byte Slot), int> instanceIds = await ReadInstanceIdsAsync(context, gameId, cancellationToken);

            DataTable amounts = BuildAmountTable(gameId, plan.ToCreate, instanceIds);

            if (amounts.Rows.Count > 0)
            {
                await BulkInsertAsync(connectionString, context.SchemaQualifiedTableName<MapResourceInstanceAmount>(), amounts, cancellationToken);
            }

            DataTable layers = BuildLayerTable(gameId, plan.ToCreate, instanceIds);

            if (layers.Rows.Count > 0)
            {
                await BulkInsertAsync(connectionString, context.SchemaQualifiedTableName<MapResourceInstanceLayer>(), layers, cancellationToken);
            }

            return ServiceResult<int>.Succeed(plan.ToCreate.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while creating the Game's MapResource instances.";
            logger.LogError(ex, "{Error} GameId: {GameId}", error, gameId);

            await TryRollBackAsync(gameId, cancellationToken);

            return ServiceResult<int>.Fail(error);
        }
    }

    /// <summary>
    /// Removes every MapResource row the Game holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three bulk copies cannot share one transaction, because the parent ids the children need are only readable once the parents are committed. A failure
    /// in the second or third copy would therefore leave instances with no amounts or no layers — a Distribution holding nothing, or a seam with no strata —
    /// which reads as a valid Game rather than a failed setup. Removing the Game's rows makes the shortfall a plain failure instead.
    /// </para>
    /// <para>
    /// Every row for the Game goes, not only the ones this run wrote. Game setup is the single caller and runs once per Game, so anything there is this run's;
    /// and the roll-back only happens where setup is failing, which leaves the Game unplayable in any case.
    /// </para>
    /// </remarks>
    /// <returns>How many rows were removed, across the three tables.</returns>
    internal static async Task<int> RollBackGameMapResourcesAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken)
    {
        // Children first: the foreign keys do not cascade, and the parents cannot go while rows point at them.
        int layers = await context.MapResourceInstanceLayers.Where(layer => layer.GameId == gameId).ExecuteDeleteAsync(cancellationToken);
        int amounts = await context.MapResourceInstanceAmounts.Where(amount => amount.GameId == gameId).ExecuteDeleteAsync(cancellationToken);
        int instances = await context.MapResourceInstances.Where(instance => instance.GameId == gameId).ExecuteDeleteAsync(cancellationToken);

        return layers + amounts + instances;
    }

    /// <summary>
    /// Rolls the Game's MapResource rows back, logging rather than throwing if that fails too.
    /// </summary>
    /// <remarks>
    /// Its own context and its own failure handling, because it runs from the catch: the error being reported is the one worth reporting, and letting a second
    /// failure replace it would lose the cause. A roll-back that cannot run leaves rows for the Game's own delete to take.
    /// </remarks>
    private async Task TryRollBackAsync(int gameId, CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            int removed = await RollBackGameMapResourcesAsync(context, gameId, cancellationToken);

            if (removed > 0)
            {
                logger.LogWarning("Rolled back {Removed} partially written MapResource rows for GameId {GameId}.", removed, gameId);
            }
        }
        catch (Exception rollBackFailure)
        {
            logger.LogError(
                rollBackFailure,
                "The partially written MapResource rows for GameId {GameId} could not be rolled back, so the Game may hold instances with no children.",
                gameId);
        }
    }

    /// <summary>
    /// The placements on the gallery map, each carrying the terrain of the tile it sits on.
    /// </summary>
    /// <remarks>
    /// Query syntax for the join: placements carry no navigation to their tile, so this is a genuine join between two tables rather than a projection through
    /// a mapped relationship. The same join is written the same way in <c>GalleryMapResourceQueries.ReadAsync</c>.
    /// </remarks>
    private static async Task<List<PlacementToInstantiate>> ReadPlacementsAsync(AscendantEmpireContext context, int campaignMapGalleryIndexId, CancellationToken cancellationToken)
    {
        return await (
            from placement in context.CampaignMapGalleryResources.AsNoTracking()
            join tile in context.CampaignMapGallery on placement.CampaignMapGalleryId equals tile.CampaignMapGalleryId
            where tile.CampaignMapGalleryIndexId == campaignMapGalleryIndexId
            orderby tile.YCoord, tile.XCoord, placement.Slot
            select new PlacementToInstantiate(
                placement.Id,
                tile.XCoord,
                tile.YCoord,
                placement.Slot,
                placement.MapResourceTypeId,
                placement.InitialQuantity,
                placement.MapResourceDistributionScenarioId,
                tile.BiomeTypeId,
                tile.CampaignMapGeographyTypeId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The named MapResourceTypes, with the habitats, scenarios and layers the rolls read.
    /// </summary>
    /// <remarks>
    /// Only the types actually placed are read: a catalogue may hold hundreds, and a map usually places a handful.
    /// </remarks>
    /// <param name="typeIds">The types to read, already deduplicated.</param>
    private static async Task<Dictionary<int, MapResourceTypeValues>> ReadTypesAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<int> typeIds,
        CancellationToken cancellationToken)
    {
        List<MapResourceType> types = await context.MapResourceTypes
            .AsNoTracking()
            .Where(type => typeIds.Contains(type.Id))
            .Include(type => type.Habitats)
            .Include(type => type.Layers)
            .Include(type => type.DistributionScenarios).ThenInclude(scenario => scenario.Amounts)
            .Include(type => type.DistributionScenarios).ThenInclude(scenario => scenario.BiomeWeights)
            .ToListAsync(cancellationToken);

        return types.ToDictionary(type => type.Id, ToMapResourceTypeValues);
    }

    private static MapResourceTypeValues ToMapResourceTypeValues(MapResourceType type)
    {
        return new MapResourceTypeValues(
            type.Id,
            type.DisplayName,
            type.IsStratified,
            type.IsDistribution,
            type.EstimationFuzzyFactorMin,
            type.EstimationFuzzyFactorMax,
            type.Habitats.ToDictionary(
                habitat => new HabitatScope(habitat.BiomeTypeId, habitat.CampaignMapGeographyTypeId),
                habitat => new HabitatValues(habitat.InitialQuantityMin, habitat.InitialQuantityMax, habitat.SpreadLimitMin, habitat.SpreadLimitMax)),
            [.. type.DistributionScenarios.Select(scenario => new ScenarioValues(
                scenario.Id,
                scenario.ScenarioNumber,
                scenario.BiomeWeights.ToDictionary(weight => weight.BiomeTypeId, weight => weight.Weight),
                [.. scenario.Amounts.Select(amount => new AmountToCreate(amount.AssetId, amount.Amount))]))],
            // Ordered by depth, so a layer's position in the collection matches the order it is worked in.
            [.. type.Layers.OrderBy(layer => layer.LayerNumber).Select(layer => new LayerValues(
                layer.LayerNumber,
                layer.AssetId,
                layer.EmptyLikelihood,
                layer.QuantityMin,
                layer.QuantityMax,
                layer.EstimationFuzzyFactorMin,
                layer.EstimationFuzzyFactorMax))]);
    }

    /// <summary>
    /// This Game's instance ids, by the tile and slot each one holds.
    /// </summary>
    /// <remarks>
    /// Keyed on tile and slot because <c>UX_MapResourceInstance_Tile_Slot</c> makes the pair unique, which is what lets a child row find its parent without
    /// the insert having returned any ids.
    /// </remarks>
    private static async Task<Dictionary<(int CampaignMapId, byte Slot), int>> ReadInstanceIdsAsync(
        AscendantEmpireContext context,
        int gameId,
        CancellationToken cancellationToken)
    {
        var rows = await context.MapResourceInstances
            .AsNoTracking()
            .Where(instance => instance.GameId == gameId)
            .Select(instance => new { instance.Id, instance.CampaignMapId, instance.Slot })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => (row.CampaignMapId, row.Slot), row => row.Id);
    }

    /// <summary>
    /// The instance rows to copy in, one per instance the rules decided.
    /// </summary>
    /// <remarks>
    /// Column names come from the entity's properties rather than literals, so a renamed property is a compile error instead of a bulk copy that fails at
    /// game setup. <c>Id</c> is left out: it is an identity column and no <c>KeepIdentity</c> option is passed, so the server assigns it.
    /// </remarks>
    internal static DataTable BuildInstanceTable(int gameId, IReadOnlyCollection<InstanceToCreate> instances)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(MapResourceInstance.GameId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstance.CampaignMapId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstance.MapResourceTypeId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstance.Slot), typeof(byte));
        table.Columns.Add(nameof(MapResourceInstance.Quantity), typeof(long));
        table.Columns.Add(nameof(MapResourceInstance.SpreadLimit), typeof(int));
        table.Columns.Add(nameof(MapResourceInstance.DormantSinceRound), typeof(int));
        table.Columns.Add(nameof(MapResourceInstance.EstimationFuzzyFactor), typeof(decimal));
        table.Columns.Add(nameof(MapResourceInstance.MapResourceDistributionScenarioId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstance.CreatedOnRound), typeof(int));

        foreach (InstanceToCreate instance in instances)
        {
            table.Rows.Add(
                gameId,
                instance.CampaignMapId,
                instance.MapResourceTypeId,
                instance.Slot,
                OrNull(instance.Quantity),
                OrNull(instance.SpreadLimit),
                // Nothing is exhausted before the Game begins.
                DBNull.Value,
                OrNull(instance.EstimationFuzzyFactor),
                OrNull(instance.MapResourceDistributionScenarioId),
                _CREATED_ON_ROUND);
        }

        return table;
    }

    /// <inheritdoc cref="BuildInstanceTable" path="/remarks"/>
    internal static DataTable BuildAmountTable(
        int gameId,
        IReadOnlyCollection<InstanceToCreate> instances,
        IReadOnlyDictionary<(int CampaignMapId, byte Slot), int> instanceIds)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(MapResourceInstanceAmount.MapResourceInstanceId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstanceAmount.AssetId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstanceAmount.GameId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstanceAmount.Amount), typeof(long));

        foreach (InstanceToCreate instance in instances)
        {
            int instanceId = instanceIds[(instance.CampaignMapId, instance.Slot)];

            foreach (AmountToCreate amount in instance.Amounts)
            {
                table.Rows.Add(instanceId, amount.AssetId, gameId, amount.Amount);
            }
        }

        return table;
    }

    /// <inheritdoc cref="BuildInstanceTable" path="/remarks"/>
    internal static DataTable BuildLayerTable(
        int gameId,
        IReadOnlyCollection<InstanceToCreate> instances,
        IReadOnlyDictionary<(int CampaignMapId, byte Slot), int> instanceIds)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(MapResourceInstanceLayer.MapResourceInstanceId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstanceLayer.LayerNumber), typeof(int));
        table.Columns.Add(nameof(MapResourceInstanceLayer.GameId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstanceLayer.AssetId), typeof(int));
        table.Columns.Add(nameof(MapResourceInstanceLayer.Quantity), typeof(long));
        table.Columns.Add(nameof(MapResourceInstanceLayer.EstimationFuzzyFactor), typeof(decimal));

        foreach (InstanceToCreate instance in instances)
        {
            int instanceId = instanceIds[(instance.CampaignMapId, instance.Slot)];

            foreach (LayerToCreate layer in instance.Layers)
            {
                table.Rows.Add(instanceId, layer.LayerNumber, gameId, OrNull(layer.AssetId), layer.Quantity, layer.EstimationFuzzyFactor);
            }
        }

        return table;
    }

    /// <summary>
    /// Copies a table into its destination, mapping by column name so the column order here need not match the table's.
    /// </summary>
    private static async Task BulkInsertAsync(string connectionString, string destinationTable, DataTable table, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = destinationTable,
            BatchSize = _BULK_INSERT_BATCH_SIZE
        };

        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table, cancellationToken);
    }

    /// <summary>
    /// Logs the placements the Game could not be given, grouped by reason and by the MapResourceType they concern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A warning rather than information for every reason: none of them fails the Game's setup, so a warning is the only thing that will tell anyone a map or a
    /// catalogue needs fixing.
    /// </para>
    /// <para>
    /// Grouped by type as well as reason, and naming the type, because "no habitat covers the tile" is not actionable without knowing which type it was said of.
    /// </para>
    /// </remarks>
    private void LogRefusals(IReadOnlyCollection<RefusedPlacement> refusals, int gameId)
    {
        var groups = refusals.GroupBy(refusal => new { refusal.Reason, refusal.MapResourceTypeId, refusal.MapResourceTypeDisplayName });

        foreach (var group in groups)
        {
            RefusedPlacement first = group.First();

            logger.LogWarning(
                "{Count} {TypeName} (MapResourceTypeId {TypeId}) placements were not created for GameId {GameId} because {Reason}. First at ({X}, {Y}).",
                group.Count(),
                group.Key.MapResourceTypeDisplayName,
                group.Key.MapResourceTypeId,
                gameId,
                group.Key.Reason,
                first.XCoord,
                first.YCoord);
        }
    }

    /// <summary>
    /// The value, or <see cref="DBNull"/>, which is what a <see cref="DataTable"/> needs in place of a null.
    /// </summary>
    private static object OrNull<T>(T? value) where T : struct
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }
}
