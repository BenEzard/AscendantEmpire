using System.Data;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <inheritdoc cref="IBiomeImpliedMapResourceService"/>
internal sealed class BiomeImpliedMapResourceService(
    AscendantEmpireContext context,
    ILogger<BiomeImpliedMapResourceService> logger
) : IBiomeImpliedMapResourceService
{
    /// <summary>
    /// How many placements are sent to the server at a time when a whole map is placed at once.
    /// </summary>
    /// <remarks>
    /// The same batch size the tile insert in <c>GeneratePerlinNoiseCampaignMap</c> uses.
    /// </remarks>
    private const int _BULK_INSERT_BATCH_SIZE = 10_000;

    /// <summary>
    /// The SQL Server error numbers for a unique index violation and a unique constraint violation.
    /// </summary>
    /// <remarks>
    /// Caught rather than prevented: see the remarks on <see cref="PlaceForGeneratedMapAsync"/>.
    /// </remarks>
    private const int _UNIQUE_INDEX_VIOLATION = 2601;
    private const int _UNIQUE_CONSTRAINT_VIOLATION = 2627;

    /// <summary>
    /// Every MapResourceType whose placement follows from a tile's Biome rather than from an author placing it.
    /// </summary>
    /// <remarks>
    /// Only Timber today. The MapResourceTypes themselves are catalogue data a database may not have seeded, so a code listed here that is absent is normal
    /// and means there is nothing to place.
    /// </remarks>
    private static readonly string[] _BIOME_IMPLIED_TYPE_CODES = [MapResourceTypeCodes.TIMBER];

    public async Task<ServiceResult<int>> PlaceForGeneratedMapAsync(int campaignMapGalleryIndexId, CancellationToken cancellationToken)
    {
        try
        {
            int placementsCreated = 0;

            foreach (string typeCode in _BIOME_IMPLIED_TYPE_CODES)
            {
                DTBiomeImpliedType? impliedType = await ReadImpliedTypeAsync(typeCode, cancellationToken);

                if (impliedType == null)
                {
                    logger.LogInformation(
                        "MapResourceType '{TypeCode}' is not in this database, so none was placed on CampaignMapGalleryIndex {IndexId}.",
                        typeCode, campaignMapGalleryIndexId);

                    continue;
                }

                IReadOnlyCollection<TileResourceState> tiles = await ReadMapTilesAsync(campaignMapGalleryIndexId, cancellationToken);

                BiomeImpliedResourcePlan plan = BiomeImpliedResourceRules.Plan(impliedType.Id, impliedType.Habitats, tiles);

                LogRefusals(plan, impliedType.DisplayName, campaignMapGalleryIndexId);

                if (plan.ToAdd.Count > 0)
                {
                    await BulkInsertPlacementsAsync(plan, cancellationToken);
                    placementsCreated += plan.ToAdd.Count;
                }

                // Nothing is deleted on a freshly generated map, because nothing was placed on it yet. Asserted rather than assumed: a plan that wanted to
                // delete here would mean the tiles were read from the wrong map.
                if (plan.PlacementIdsToDelete.Count > 0)
                {
                    logger.LogWarning(
                        "Placing {TypeName} on newly generated CampaignMapGalleryIndex {IndexId} wanted to delete {DeleteCount} existing placements, which a "
                            + "new map should not have. They were left alone.",
                        impliedType.DisplayName, campaignMapGalleryIndexId, plan.PlacementIdsToDelete.Count);
                }
            }

            return ServiceResult<int>.Succeed(placementsCreated);
        }
        catch (Exception ex) when (IsUniquenessViolation(ex))
        {
            // Two generations for the same map at once would collide between reading the tiles and writing the placements. Nothing enqueues that today, and
            // swallowing it is right for a pass the map does not depend on.
            logger.LogWarning(ex,
                "Biome-implied MapResources collided with placements already on CampaignMapGalleryIndex {IndexId}.",
                campaignMapGalleryIndexId);

            return ServiceResult<int>.Fail("Some Biome-implied MapResources were already placed on this map.");
        }
        catch (Exception ex)
        {
            const string error = "Error placing the MapResources the map's Biomes imply.";
            logger.LogError(ex, "{Error} CampaignMapGalleryIndex {IndexId}.", error, campaignMapGalleryIndexId);

            return ServiceResult<int>.Fail(error);
        }
    }

    public async Task<ServiceResult<BiomeImpliedResourcePlan>> PlanPaintAsync(
        IReadOnlyCollection<CampaignMapGallery> paintedTiles,
        BiomeType? paintedBiome,
        CampaignMapGeographyType? paintedGeography,
        CancellationToken cancellationToken)
    {
        if (paintedTiles.Count == 0)
        {
            return ServiceResult<BiomeImpliedResourcePlan>.Succeed(BiomeImpliedResourcePlan.None);
        }

        try
        {
            // One implied type today, so one plan. A second would need the plans merged, and the slot each one takes decided against the other's - which is
            // why this returns after the first rather than quietly planning only the last.
            foreach (string typeCode in _BIOME_IMPLIED_TYPE_CODES)
            {
                DTBiomeImpliedType? impliedType = await ReadImpliedTypeAsync(typeCode, cancellationToken);

                if (impliedType == null)
                {
                    continue;
                }

                IReadOnlyCollection<TileResourceState> tiles = await ReadPaintedTilesAsync(
                    paintedTiles: paintedTiles,
                    paintedBiome: paintedBiome,
                    paintedGeography: paintedGeography,
                    cancellationToken: cancellationToken);

                return ServiceResult<BiomeImpliedResourcePlan>.Succeed(
                    BiomeImpliedResourceRules.Plan(impliedType.Id, impliedType.Habitats, tiles));
            }

            return ServiceResult<BiomeImpliedResourcePlan>.Succeed(BiomeImpliedResourcePlan.None);
        }
        catch (Exception ex)
        {
            const string error = "Error working out the MapResources the painted tiles' Biomes imply.";
            logger.LogError(ex, error);

            return ServiceResult<BiomeImpliedResourcePlan>.Fail(error);
        }
    }

    public async Task ApplyPaintPlanAsync(BiomeImpliedResourcePlan plan, CancellationToken cancellationToken)
    {
        if (plan.PlacementIdsToDelete.Count > 0)
        {
            int[] placementIdsToDelete = [.. plan.PlacementIdsToDelete];

            // Deleted straight from the table rather than by attaching a stub to the change tracker. The tracker may already hold the very row being deleted -
            // a tile painted into a wooded Biome and then out of it again within one scope is exactly that - and attaching a second instance of a key it is
            // already tracking throws. The caller's transaction covers this statement as well as the save below.
            await context.CampaignMapGalleryResources
                .Where(placement => placementIdsToDelete.Contains(placement.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        foreach (PlacementToAdd placement in plan.ToAdd)
        {
            context.CampaignMapGalleryResources.Add(new CampaignMapGalleryResource
            {
                CampaignMapGalleryId = placement.CampaignMapGalleryId,
                MapResourceTypeId = plan.MapResourceTypeId,
                Slot = placement.Slot
            });
        }
    }

    /// <summary>
    /// A biome-implied MapResourceType and the habitats that say where it belongs.
    /// </summary>
    private sealed record DTBiomeImpliedType(int Id, string DisplayName, IReadOnlyCollection<HabitatScope> Habitats);

    /// <summary>
    /// Reads one MapResourceType by its Code, with its habitats, or <see langword="null"/> when this database has not seeded it.
    /// </summary>
    private async Task<DTBiomeImpliedType?> ReadImpliedTypeAsync(string typeCode, CancellationToken cancellationToken)
    {
        var impliedType = await context.MapResourceTypes
            .AsNoTracking()
            .Where(type => type.Code == typeCode)
            .Select(type => new { type.Id, type.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);

        if (impliedType == null)
        {
            return null;
        }

        List<HabitatScope> habitats = await context.MapResourceHabitats
            .AsNoTracking()
            .Where(habitat => habitat.MapResourceTypeId == impliedType.Id)
            .Select(habitat => new HabitatScope(habitat.BiomeTypeId, habitat.CampaignMapGeographyTypeId))
            .ToListAsync(cancellationToken);

        return new DTBiomeImpliedType(impliedType.Id, impliedType.DisplayName, habitats);
    }

    /// <summary>
    /// Reads every tile of one map, with whatever each already holds.
    /// </summary>
    /// <remarks>
    /// Scalars only. A 200x200 map is 40,000 tiles, and loading them as entities would track every one of them for a pass that only decides and writes.
    /// </remarks>
    private async Task<IReadOnlyCollection<TileResourceState>> ReadMapTilesAsync(int campaignMapGalleryIndexId, CancellationToken cancellationToken)
    {
        var tiles = await context.CampaignMapGallery
            .AsNoTracking()
            .Where(tile => tile.CampaignMapGalleryIndexId == campaignMapGalleryIndexId)
            .Select(tile => new
            {
                tile.CampaignMapGalleryId,
                tile.XCoord,
                tile.YCoord,
                tile.BiomeTypeId,
                tile.CampaignMapGeographyTypeId
            })
            .ToListAsync(cancellationToken);

        Dictionary<int, List<TilePlacementState>> placementsByTile = await ReadPlacementsByTileAsync(
            context.CampaignMapGalleryResources
                .Where(placement => context.CampaignMapGallery
                    .Any(tile => tile.CampaignMapGalleryId == placement.CampaignMapGalleryId
                        && tile.CampaignMapGalleryIndexId == campaignMapGalleryIndexId)),
            cancellationToken);

        return [.. tiles.Select(tile => new TileResourceState(
            tile.CampaignMapGalleryId,
            tile.XCoord,
            tile.YCoord,
            tile.BiomeTypeId,
            tile.CampaignMapGeographyTypeId,
            placementsByTile.TryGetValue(tile.CampaignMapGalleryId, out List<TilePlacementState>? placements) ? placements : []))];
    }

    /// <summary>
    /// Turns the tiles a paint covers into the terrain they are about to have, with whatever each already holds.
    /// </summary>
    /// <remarks>
    /// A null painted Biome or Geography leaves that half of the tile's terrain as it is, which is how a paint that changes only one of the two is decided
    /// against the other's current value.
    /// </remarks>
    private async Task<IReadOnlyCollection<TileResourceState>> ReadPaintedTilesAsync(
        IReadOnlyCollection<CampaignMapGallery> paintedTiles,
        BiomeType? paintedBiome,
        CampaignMapGeographyType? paintedGeography,
        CancellationToken cancellationToken)
    {
        int[] paintedTileIds = [.. paintedTiles.Select(tile => tile.CampaignMapGalleryId)];

        Dictionary<int, List<TilePlacementState>> placementsByTile = await ReadPlacementsByTileAsync(
            context.CampaignMapGalleryResources.Where(placement => paintedTileIds.Contains(placement.CampaignMapGalleryId)),
            cancellationToken);

        return [.. paintedTiles.Select(tile => new TileResourceState(
            tile.CampaignMapGalleryId,
            tile.XCoord,
            tile.YCoord,
            paintedBiome ?? tile.BiomeTypeId,
            paintedGeography ?? tile.CampaignMapGeographyTypeId,
            placementsByTile.TryGetValue(tile.CampaignMapGalleryId, out List<TilePlacementState>? placements) ? placements : []))];
    }

    /// <summary>
    /// Reads the placements a query selects, grouped by the tile they sit on.
    /// </summary>
    /// <remarks>
    /// This is where author intent is decided, so the rules never see the nullable columns it is derived from: a pinned scenario or a chosen quantity is
    /// something the map editor said about this one tile, which generating or repainting must not throw away.
    /// </remarks>
    private static async Task<Dictionary<int, List<TilePlacementState>>> ReadPlacementsByTileAsync(
        IQueryable<CampaignMapGalleryResource> placements,
        CancellationToken cancellationToken)
    {
        var rows = await placements
            .AsNoTracking()
            .Select(placement => new
            {
                placement.Id,
                placement.CampaignMapGalleryId,
                placement.MapResourceTypeId,
                placement.Slot,
                placement.InitialQuantity,
                placement.MapResourceDistributionScenarioId
            })
            .ToListAsync(cancellationToken);

        var byTile = new Dictionary<int, List<TilePlacementState>>();

        foreach (var row in rows)
        {
            bool carriesAuthorIntent = row.MapResourceDistributionScenarioId != null || row.InitialQuantity != null;

            if (byTile.TryGetValue(row.CampaignMapGalleryId, out List<TilePlacementState>? onTile) == false)
            {
                onTile = [];
                byTile[row.CampaignMapGalleryId] = onTile;
            }

            onTile.Add(new TilePlacementState(row.Id, row.MapResourceTypeId, row.Slot, carriesAuthorIntent));
        }

        return byTile;
    }

    /// <summary>
    /// Writes a whole map's new placements in one pass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SqlBulkCopy"/> rather than EF, because a blank map of Forest is one placement per tile - 40,000 of them on a 200x200 map - and
    /// <c>SaveChangesAsync</c> would batch those as inserts reading back 40,000 identity values this path has no use for. It is a typed client API with no
    /// SQL text, and the tile insert in <c>GeneratePerlinNoiseCampaignMap</c> writes the same table family the same way.
    /// </para>
    /// <para>
    /// <c>Id</c> is left out of the table and the column mappings, and no <c>KeepIdentity</c> option is passed, so the server assigns it. The tile key is
    /// allocated by its writer; this one is not.
    /// </para>
    /// <para>
    /// An <c>INSERT ... SELECT</c> joining the habitats would be one round trip and inherently idempotent, but it would put the habitat and free-slot rules
    /// in T-SQL where the map editor's brush could not share them. Both callers deciding in <see cref="BiomeImpliedResourceRules"/> is the point.
    /// </para>
    /// </remarks>
    private async Task BulkInsertPlacementsAsync(BiomeImpliedResourcePlan plan, CancellationToken cancellationToken)
    {
        DataTable table = BuildPlacementTable(plan);

        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = context.SchemaQualifiedTableName<CampaignMapGalleryResource>(),
            BatchSize = _BULK_INSERT_BATCH_SIZE
        };

        // Mapped by column name, so the order the columns were added in cannot matter.
        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table, cancellationToken);
    }

    /// <summary>
    /// The placement rows to copy in, one per tile the plan decided.
    /// </summary>
    /// <remarks>
    /// Column names come from the entity's properties rather than literals, so a renamed property is a compile error instead of a bulk copy that fails when a
    /// map is generated. The nullable authored quantity and pinned scenario are left out, those being the editor's to set and not this path's.
    /// </remarks>
    internal static DataTable BuildPlacementTable(BiomeImpliedResourcePlan plan)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(CampaignMapGalleryResource.CampaignMapGalleryId), typeof(int));
        table.Columns.Add(nameof(CampaignMapGalleryResource.MapResourceTypeId), typeof(int));
        table.Columns.Add(nameof(CampaignMapGalleryResource.Slot), typeof(byte));

        foreach (PlacementToAdd placement in plan.ToAdd)
        {
            table.Rows.Add(placement.CampaignMapGalleryId, plan.MapResourceTypeId, placement.Slot);
        }

        return table;
    }

    /// <summary>
    /// Logs the tiles a generated map could not be given its implied MapResources on.
    /// </summary>
    /// <remarks>
    /// A full tile is not an error: the author filled both of its slots, and the MapResource the Biome implies is a convenience.
    /// </remarks>
    private void LogRefusals(BiomeImpliedResourcePlan plan, string typeDisplayName, int campaignMapGalleryIndexId)
    {
        int fullTiles = plan.Refusals.Count(refusal => refusal.Reason == BiomeImpliedResourceRefusal.NoFreeSlot);

        if (fullTiles > 0)
        {
            logger.LogInformation(
                "{TypeName} was not placed on {FullTileCount} tiles of CampaignMapGalleryIndex {IndexId} because both of their slots were already taken.",
                typeDisplayName, fullTiles, campaignMapGalleryIndexId);
        }
    }

    /// <summary>
    /// Whether an exception is a unique index or unique constraint violation, however it is wrapped.
    /// </summary>
    private static bool IsUniquenessViolation(Exception exception)
    {
        return exception is SqlException sqlException
            ? sqlException.Number is _UNIQUE_INDEX_VIOLATION or _UNIQUE_CONSTRAINT_VIOLATION
            : exception.InnerException != null && IsUniquenessViolation(exception.InnerException);
    }
}
