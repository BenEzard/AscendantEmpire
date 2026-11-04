using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.BackendContracts.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests how the storage class upsert matches existing rows and what it writes.
/// </summary>
/// <remarks>
/// In-memory: the handler sticks to LINQ, <c>Add</c> and <c>SaveChanges</c>, and every rule exercised here is enforced in the handler rather than by a
/// database constraint. The unique indexes are deliberately not exercised - in-memory cannot reproduce them, which is also why the handler checks for
/// duplicate codes itself.
/// </remarks>
[TestFixture]
internal class UpsertStorageClassesHandlerTests : InMemoryTestBase
{
    private AscendantEmpireContext _context = null!;
    private UpsertStorageClassesHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new UpsertStorageClassesHandler(new NullLogger<UpsertStorageClassesHandler>(), _context);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    [Test, Description("An empty request is refused, because a caller asking to upsert nothing has made a mistake rather than expressed an intent.")]
    public async Task ShouldRefuseWhenNoClassesAreProvided()
    {
        ServiceResultBase result = await _handler.Handle(new UpsertStorageClassesRequest([]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("A class absent from the database is inserted, so a first seed fills the catalogue.")]
    public async Task ShouldInsertWhenTheCodeIsNew()
    {
        await UpsertAsync([Class("Armoury", "Armoury"), Class("Granary", "Granary")]);

        Assert.That(
            _context.StorageClasses.Select(storageClass => storageClass.Code).OrderBy(code => code),
            Is.EqualTo(["Armoury", "Granary"]));
    }

    [Test, Description("A class already present is renamed in place rather than duplicated, which is what makes re-seeding safe.")]
    public async Task ShouldRenameInPlaceWhenTheCodeAlreadyExists()
    {
        await UpsertAsync([Class("FuelDump", "Fuel Dump")]);
        await UpsertAsync([Class("FuelDump", "Oil Dump")]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.StorageClasses.Count(), Is.EqualTo(1));
            Assert.That(_context.StorageClasses.Single().DisplayName, Is.EqualTo("Oil Dump"));
        }
    }

    [Test, Description("A code differing from the stored one only in case matches it, because the unique index is case-insensitive and an ordinal match would queue an insert the index then rejects.")]
    public async Task ShouldMatchExistingRowsCaseInsensitively()
    {
        await UpsertAsync([Class("Armoury", "Armoury")]);
        await UpsertAsync([Class("ARMOURY", "Weapon Store")]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.StorageClasses.Count(), Is.EqualTo(1));
            Assert.That(_context.StorageClasses.Single().DisplayName, Is.EqualTo("Weapon Store"));

            // Code is the match key, not the thing matched, so the casing already stored survives a request that spells it differently.
            Assert.That(_context.StorageClasses.Single().Code, Is.EqualTo("Armoury"));
        }
    }

    [Test, Description("Two entries resolving to the same code are refused with the code named, rather than passing validation and colliding at the database.")]
    public async Task ShouldRefuseWhenTheRequestRepeatsACode()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertStorageClassesRequest([Class("Armoury", "Armoury"), Class("armoury", "Weapon Store")]),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Armoury").IgnoreCase);
        }
    }

    [Test, Description("A blank code is refused and the offending display name is named, since Code is the match key and cannot be derived here.")]
    public async Task ShouldRefuseWhenACodeIsBlank()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertStorageClassesRequest([Class("   ", "Armoury")]),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Armoury"));
        }
    }

    [Test, Description("The real seed data upserts cleanly, so a code or display name clash in the authored catalogue is caught here rather than at seed time.")]
    public async Task ShouldUpsertTheRealSeedData()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertStorageClassesRequest(StorageClassSeedData.Build()),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(_context.StorageClasses.Count(), Is.EqualTo(StorageClassSeedData.Build().Count));
        }
    }

    [Test, Description("Re-running the real seed data converges rather than duplicating, which is the property the seed's idempotence rests on.")]
    public async Task ShouldConvergeWhenTheRealSeedDataRunsTwice()
    {
        await UpsertAsync(StorageClassSeedData.Build());
        await UpsertAsync(StorageClassSeedData.Build());

        Assert.That(_context.StorageClasses.Count(), Is.EqualTo(StorageClassSeedData.Build().Count));
    }

    private static UpsertStorageClassDto Class(string code, string displayName)
    {
        return new UpsertStorageClassDto { Code = code, DisplayName = displayName };
    }

    private async Task UpsertAsync(IReadOnlyCollection<UpsertStorageClassDto> storageClasses)
    {
        ServiceResultBase result = await _handler.Handle(new UpsertStorageClassesRequest(storageClasses), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }
}
