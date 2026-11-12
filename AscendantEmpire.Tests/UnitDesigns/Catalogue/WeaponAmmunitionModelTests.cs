using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Tests.UnitDesigns.Catalogue;

/// <summary>
/// Pins where a weapon's ammunition options live and how they are keyed.
/// </summary>
/// <remarks>
/// <para>
/// A table name, a schema or a unique index is relational metadata rather than anything a compiler checks, so a rename would otherwise surface only as a
/// failed migration or a broken query. The options sit in <c>unitdesigns</c> with the rest of the component catalogue, and reach across to an
/// <c>assets.Asset</c>.
/// </para>
/// <para>
/// No database of any kind: only the model is read, built from options that are never connected. The provider still has to be SQL Server, since table and
/// schema names are relational metadata the in-memory provider does not supply.
/// </para>
/// </remarks>
internal sealed class WeaponAmmunitionModelTests
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

    [Test, Description("Pins the ammunition table's schema, key and uniqueness so a rename cannot pass unnoticed.")]
    public void Model_MapsWeaponAmmunitionToExpectedTableAndKeys()
    {
        IEntityType ammunition = GetEntityType<UnitComponentWeaponAmmunition>();

        IIndex? weaponAndAsset = ammunition.GetIndexes()
            .FirstOrDefault(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(UnitComponentWeaponAmmunition.UnitComponentWeaponId), nameof(UnitComponentWeaponAmmunition.AssetId)]));

        Assert.Multiple(() =>
        {
            Assert.That(ammunition.GetTableName(), Is.EqualTo("UnitComponentWeaponAmmunition"));
            Assert.That(ammunition.GetSchema(), Is.EqualTo("unitdesigns"));
            Assert.That(GetPrimaryKeyProperties(ammunition), Is.EqualTo(new[] { nameof(UnitComponentWeaponAmmunition.Id) }));

            Assert.That(weaponAndAsset, Is.Not.Null, "No index covers (UnitComponentWeaponId, AssetId).");
            Assert.That(weaponAndAsset!.IsUnique, Is.True, "One weapon lists an Asset at most once, and the merge keys on the Asset.");
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
