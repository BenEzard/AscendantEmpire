using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Pins where the supply catalogue lives and how it is keyed.
/// </summary>
/// <remarks>
/// <para>
/// A table name, a schema or a composite key is relational metadata rather than anything a compiler checks, so a rename would otherwise surface only as a
/// failed migration or a broken query. The sets sit in <c>assets</c> because they group Assets; the requirement sits in <c>unitdesigns</c> because it is a
/// unit design concern.
/// </para>
/// <para>
/// No database of any kind: only the model is read, built from options that are never connected. The provider still has to be SQL Server, since table and
/// schema names are relational metadata the in-memory provider does not supply.
/// </para>
/// </remarks>
internal sealed class SupplySetModelTests
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

    #region Mapping

    [Test, Description("Pins the supply catalogue's tables, schemas and keys so a rename cannot pass unnoticed.")]
    public void Model_MapsSupplyCatalogueToExpectedTablesAndKeys()
    {
        IEntityType supplySet = GetEntityType<SupplySet>();
        IEntityType supplySetAsset = GetEntityType<SupplySetAsset>();
        IEntityType requirement = GetEntityType<UnitComponentSupplyRequirement>();

        Assert.Multiple(() =>
        {
            Assert.That(supplySet.GetTableName(), Is.EqualTo("SupplySet"));
            Assert.That(supplySet.GetSchema(), Is.EqualTo("assets"));

            Assert.That(supplySetAsset.GetTableName(), Is.EqualTo("SupplySetAsset"));
            Assert.That(supplySetAsset.GetSchema(), Is.EqualTo("assets"));
            Assert.That(GetPrimaryKeyProperties(supplySetAsset), Is.EqualTo(new[] { nameof(SupplySetAsset.SupplySetId), nameof(SupplySetAsset.AssetId) }));

            Assert.That(requirement.GetTableName(), Is.EqualTo("UnitComponentSupplyRequirement"));
            Assert.That(requirement.GetSchema(), Is.EqualTo("unitdesigns"));
            Assert.That(GetPrimaryKeyProperties(requirement), Is.EqualTo(new[] { nameof(UnitComponentSupplyRequirement.Id) }));
        });
    }

    [Test, Description("A component cannot require the same supply set twice, enforced by a unique index rather than by the reconciler alone.")]
    public void Model_KeepsOneRequirementPerComponentAndSet()
    {
        IEntityType requirement = GetEntityType<UnitComponentSupplyRequirement>();

        IIndex? index = requirement.GetIndexes()
            .FirstOrDefault(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(UnitComponentSupplyRequirement.UnitComponentId), nameof(UnitComponentSupplyRequirement.SupplySetId)]));

        Assert.Multiple(() =>
        {
            Assert.That(index, Is.Not.Null, "No index covers (UnitComponentId, SupplySetId).");
            Assert.That(index!.IsUnique, Is.True, "Two rows for one set would double the need, and the merge keys on the set.");
        });
    }

    [Test, Description("SelectionWeight carries no store default, since one would make an explicit zero indistinguishable from an unset value on insert.")]
    public void Model_LeavesSelectionWeightWithoutAStoreDefault()
    {
        IProperty selectionWeight = GetEntityType<SupplySetAsset>().GetProperty(nameof(SupplySetAsset.SelectionWeight));

        Assert.Multiple(() =>
        {
            // TryGetDefaultValue rather than GetDefaultValue, which boxes the CLR default and so reads 0 whether or not a default was configured. This is the
            // same call the migration pipeline makes to decide whether to emit a defaultValue.
            Assert.That(selectionWeight.TryGetDefaultValue(out _), Is.False);
            Assert.That(selectionWeight.ValueGenerated, Is.EqualTo(ValueGenerated.Never));
        });
    }

    #endregion

    #region Helpers

    private IEntityType GetEntityType<TEntity>() where TEntity : class
    {
        return _context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not a mapped entity.");
    }

    private static string[] GetPrimaryKeyProperties(IEntityType entity)
    {
        IKey key = entity.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{entity.DisplayName()} has no primary key.");

        return [.. key.Properties.Select(property => property.Name)];
    }

    #endregion
}
