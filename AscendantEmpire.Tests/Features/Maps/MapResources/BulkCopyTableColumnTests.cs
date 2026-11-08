using System.Data;

using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins the <c>DataTable</c> every MapResource bulk copy builds against the columns the EF model says the destination table has.
/// </summary>
/// <remarks>
/// <para>
/// A bulk copy maps its columns by name, so a name that drifts from the model is not a compile error — it is a failure when a Game is set up or a map is
/// generated. <c>nameof</c> on the entity's properties covers a renamed property; these tests cover what it cannot, a column renamed with
/// <c>HasColumnName</c> or a new NOT NULL column added without a value.
/// </para>
/// <para>
/// No database of any kind: only the model is read, and a model is built from options that are never connected. The provider still has to be SQL Server, since
/// table and column names are relational metadata that the in-memory provider does not supply.
/// </para>
/// </remarks>
internal sealed class BulkCopyTableColumnTests
{
    /// <summary>
    /// A connection string that is only ever parsed, never opened, so the model can be built without a server.
    /// </summary>
    private const string _UNUSED_CONNECTION = "Server=(unused);Database=(unused);Trusted_Connection=True;";

    private AscendantEmpireContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        DbContextOptions<AscendantEmpireContext> options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseSqlServer(_UNUSED_CONNECTION)
            .Options;

        _context = new AscendantEmpireContext(options);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test, Description("Every column the instance bulk copy writes is a real column of game.MapResourceInstance, so none can be silently misnamed.")]
    public void ShouldWriteOnlyRealColumnsOnInstanceTable()
    {
        DataTable table = MapResourceInstantiationService.BuildInstanceTable(gameId: 1, []);

        AssertColumnsExist<MapResourceInstance>(table);
    }

    [Test, Description("Every column the amount bulk copy writes is a real column of game.MapResourceInstanceAmount.")]
    public void ShouldWriteOnlyRealColumnsOnAmountTable()
    {
        DataTable table = MapResourceInstantiationService.BuildAmountTable(gameId: 1, [], new Dictionary<(int, byte), int>());

        AssertColumnsExist<MapResourceInstanceAmount>(table);
    }

    [Test, Description("Every column the layer bulk copy writes is a real column of game.MapResourceInstanceLayer.")]
    public void ShouldWriteOnlyRealColumnsOnLayerTable()
    {
        DataTable table = MapResourceInstantiationService.BuildLayerTable(gameId: 1, [], new Dictionary<(int, byte), int>());

        AssertColumnsExist<MapResourceInstanceLayer>(table);
    }

    [Test, Description("Every column the Biome-implied placement bulk copy writes is a real column of mapgallery.CampaignMapGalleryResource.")]
    public void ShouldWriteOnlyRealColumnsOnPlacementTable()
    {
        DataTable table = BiomeImpliedMapResourceService.BuildPlacementTable(EmptyPlacementPlan());

        AssertColumnsExist<CampaignMapGalleryResource>(table);
    }

    [Test, Description("The instance bulk copy supplies every column the table requires, so adding a NOT NULL column cannot be forgotten.")]
    public void ShouldSupplyEveryRequiredColumnOnInstanceTable()
    {
        DataTable table = MapResourceInstantiationService.BuildInstanceTable(gameId: 1, []);

        AssertRequiredColumnsSupplied<MapResourceInstance>(table);
    }

    [Test, Description("The amount bulk copy supplies every column that table requires.")]
    public void ShouldSupplyEveryRequiredColumnOnAmountTable()
    {
        DataTable table = MapResourceInstantiationService.BuildAmountTable(gameId: 1, [], new Dictionary<(int, byte), int>());

        AssertRequiredColumnsSupplied<MapResourceInstanceAmount>(table);
    }

    [Test, Description("The layer bulk copy supplies every column that table requires.")]
    public void ShouldSupplyEveryRequiredColumnOnLayerTable()
    {
        DataTable table = MapResourceInstantiationService.BuildLayerTable(gameId: 1, [], new Dictionary<(int, byte), int>());

        AssertRequiredColumnsSupplied<MapResourceInstanceLayer>(table);
    }

    [Test, Description("The placement bulk copy supplies every column that table requires.")]
    public void ShouldSupplyEveryRequiredColumnOnPlacementTable()
    {
        DataTable table = BiomeImpliedMapResourceService.BuildPlacementTable(EmptyPlacementPlan());

        AssertRequiredColumnsSupplied<CampaignMapGalleryResource>(table);
    }

    /// <summary>
    /// A plan that places nothing. These tests read the table's shape, which no row affects.
    /// </summary>
    private static BiomeImpliedResourcePlan EmptyPlacementPlan()
    {
        return new BiomeImpliedResourcePlan(MapResourceTypeId: 1, ToAdd: [], PlacementIdsToDelete: [], Refusals: []);
    }

    /// <summary>
    /// Fails if the table carries a column the entity's table does not have.
    /// </summary>
    private void AssertColumnsExist<TEntity>(DataTable table) where TEntity : class
    {
        List<string> modelColumns = [.. ColumnNames<TEntity>()];
        List<string> written = [.. table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(written, Is.Not.Empty, "A table with no columns would make the rest of this assertion vacuous.");
            Assert.That(
                written,
                Is.SubsetOf(modelColumns),
                $"A column the bulk copy writes is not a column of {typeof(TEntity).Name}, so the copy would fail at run time.");
        }
    }

    /// <summary>
    /// Fails if the table omits a column the database requires and does not generate itself.
    /// </summary>
    /// <remarks>
    /// An identity key and anything else the store generates are excluded: the bulk copy deliberately leaves those to the server.
    /// </remarks>
    private void AssertRequiredColumnsSupplied<TEntity>(DataTable table) where TEntity : class
    {
        IEntityType entity = Entity<TEntity>();
        StoreObjectIdentifier store = StoreObject<TEntity>();

        List<string> required =
        [
            .. entity.GetProperties()
                .Where(property => property.IsNullable == false && property.ValueGenerated == ValueGenerated.Never)
                .Select(property => property.GetColumnName(store))
                .OfType<string>()
        ];

        List<string> written = [.. table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)];

        Assert.That(
            required.Except(written),
            Is.Empty,
            $"{typeof(TEntity).Name} requires a column the bulk copy does not supply, so every row it writes would be rejected.");
    }

    private IEnumerable<string> ColumnNames<TEntity>() where TEntity : class
    {
        StoreObjectIdentifier store = StoreObject<TEntity>();

        return Entity<TEntity>().GetProperties().Select(property => property.GetColumnName(store)).OfType<string>();
    }

    private IEntityType Entity<TEntity>() where TEntity : class
    {
        return _context.Model.FindEntityType(typeof(TEntity)) ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");
    }

    private StoreObjectIdentifier StoreObject<TEntity>() where TEntity : class
    {
        return StoreObjectIdentifier.Create(Entity<TEntity>(), StoreObjectType.Table)
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} maps to no table.");
    }
}
