using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests how the experience type upsert matches existing rows and what it writes.
/// </summary>
/// <remarks>
/// In-memory: the handler sticks to LINQ, <c>Add</c> and <c>SaveChanges</c>, and every rule exercised here is enforced in the handler rather than by a
/// database constraint. The unique indexes are deliberately not exercised — in-memory cannot reproduce them, which is also why the handler checks duplicate
/// codes and sort orders itself.
/// </remarks>
[TestFixture]
internal class UpsertExperienceTypesHandlerTests : InMemoryTestBase
{
    private AscendantEmpireContext _context = null!;
    private UpsertExperienceTypesHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new UpsertExperienceTypesHandler(new NullLogger<UpsertExperienceTypesHandler>(), _context);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    [Test, Description("Codes are stored verbatim rather than derived, because each producing system resolves its own concept against that exact string.")]
    public async Task ShouldStoreSuppliedCodesVerbatim()
    {
        await UpsertAsync([Type("Biome.Jungle", "Jungle"), Type("Extraction.Mining", "Mining")]);

        Assert.That(
            _context.ExperienceTypes.Select(experienceType => experienceType.Code).OrderBy(code => code),
            Is.EqualTo(new[] { "Biome.Jungle", "Extraction.Mining" }));
    }

    [Test, Description("Sort order is derived from list position in steps of ten, so the file's order is the game's order with no second set of numbers.")]
    public async Task ShouldDeriveSortOrderFromListPosition()
    {
        await UpsertAsync([Type("Exploration", "Exploration"), Type("Biome.Jungle", "Jungle")]);

        Assert.Multiple(() =>
        {
            Assert.That(_context.ExperienceTypes.Single(e => e.Code == "Exploration").SortOrder, Is.EqualTo(10));
            Assert.That(_context.ExperienceTypes.Single(e => e.Code == "Biome.Jungle").SortOrder, Is.EqualTo(20));
        });
    }

    [Test, Description("Re-running with a changed display name renames the existing row rather than inserting a second one, which is what makes reseeding safe.")]
    public async Task ShouldRenameRatherThanDuplicateWhenTheCodeIsUnchanged()
    {
        await UpsertAsync([Type("Biome.Jungle", "Jungle")]);
        await UpsertAsync([Type("Biome.Jungle", "Rainforest")]);

        Assert.Multiple(() =>
        {
            Assert.That(_context.ExperienceTypes.Count(), Is.EqualTo(1));
            Assert.That(_context.ExperienceTypes.Single().DisplayName, Is.EqualTo("Rainforest"));
        });
    }

    [Test, Description("An omitted description leaves any existing one alone, so a partial update cannot silently blank authored text.")]
    public async Task ShouldLeaveExistingDescriptionAloneWhenNotSupplied()
    {
        await UpsertAsync([Type("Biome.Jungle", "Jungle") with { Description = "Hard going." }]);
        await UpsertAsync([Type("Biome.Jungle", "Jungle")]);

        Assert.That(_context.ExperienceTypes.Single().Description, Is.EqualTo("Hard going."));
    }

    [Test, Description("An empty request fails rather than silently succeeding, so a misconfigured seed step is reported instead of looking like a no-op.")]
    public async Task ShouldFailWhenNoExperienceTypesAreSupplied()
    {
        ServiceResultBase result = await _handler.Handle(new UpsertExperienceTypesRequest([]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("Two entries sharing a code are rejected, since in-memory cannot reproduce the unique index that would otherwise catch it.")]
    public async Task ShouldRejectDuplicateCodesWithinOneRequest()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertExperienceTypesRequest([Type("Biome.Jungle", "Jungle"), Type("Biome.Jungle", "Rainforest")]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Biome.Jungle"));
        });
    }

    [Test, Description("Two entries sharing an explicit sort order are rejected, because the catalogue's sort order is unique across the whole table.")]
    public async Task ShouldRejectDuplicateSortOrdersWithinOneRequest()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertExperienceTypesRequest(
            [
                Type("Biome.Jungle", "Jungle") with { SortOrder = 10 },
                Type("Exploration", "Exploration") with { SortOrder = 10 }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("sort order"));
        });
    }

    [Test, Description("A blank code is rejected, naming the display name, since the code is the cross-reference key and cannot be defaulted.")]
    public async Task ShouldRejectABlankCode()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertExperienceTypesRequest([Type("   ", "Jungle")]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Jungle"));
        });
    }

    [Test, Description("The real seed data upserts cleanly, so a code or sort-order clash in the authored catalogue is caught here rather than at seed time.")]
    public async Task ShouldUpsertTheRealSeedData()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertExperienceTypesRequest(ExperienceTypeSeedData.Build()),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(_context.ExperienceTypes.Count(), Is.EqualTo(ExperienceTypeSeedData.Build().Count));
        });
    }

    private static UpsertExperienceTypeDto Type(string code, string displayName)
    {
        return new UpsertExperienceTypeDto { Code = code, DisplayName = displayName };
    }

    private async Task UpsertAsync(IReadOnlyCollection<UpsertExperienceTypeDto> experienceTypes)
    {
        ServiceResultBase result = await _handler.Handle(new UpsertExperienceTypesRequest(experienceTypes), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }
}
