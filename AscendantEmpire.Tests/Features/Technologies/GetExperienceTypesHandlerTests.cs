using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.BackendContracts.Technologies.GetExperienceTypes;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests what the experience type read returns and in what order.
/// </summary>
/// <remarks>
/// In-memory: the handler is a LINQ projection over the context. Writes go through the real upsert handler rather than being hand-built.
/// </remarks>
[TestFixture]
internal class GetExperienceTypesHandlerTests : InMemoryTestBase
{
    private AscendantEmpireContext _context = null!;
    private GetExperienceTypesHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new GetExperienceTypesHandler(new NullLogger<GetExperienceTypesHandler>(), DbFactory);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    [Test, Description("An empty catalogue returns an empty collection rather than failing, so a client can call this before anything is seeded.")]
    public async Task ShouldReturnAnEmptyCollectionWhenNothingIsSeeded()
    {
        Assert.That(await GetAsync(), Is.Empty);
    }

    [Test, Description("Every authored field comes back, so the editor can render and edit a row without a second call.")]
    public async Task ShouldReturnEveryAuthoredField()
    {
        await UpsertAsync(
        [
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Jungle",
                DisplayName = "Jungle",
                Description = "Hard going.",
                IconPath = "/experience/jungle.png",
                SortOrder = 40
            }
        ]);

        ExperienceTypeDetail experienceType = (await GetAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(experienceType.Code, Is.EqualTo("Biome.Jungle"));
            Assert.That(experienceType.DisplayName, Is.EqualTo("Jungle"));
            Assert.That(experienceType.Description, Is.EqualTo("Hard going."));
            Assert.That(experienceType.IconPath, Is.EqualTo("/experience/jungle.png"));
            Assert.That(experienceType.SortOrder, Is.EqualTo(40));
        });
    }

    [Test, Description("Types come back in sort order, so a client can render the list without re-sorting it.")]
    public async Task ShouldOrderBySortOrder()
    {
        await UpsertAsync(
        [
            new UpsertExperienceTypeDto { Code = "Second", DisplayName = "Second", SortOrder = 20 },
            new UpsertExperienceTypeDto { Code = "First", DisplayName = "First", SortOrder = 10 }
        ]);

        Assert.That((await GetAsync()).Select(experienceType => experienceType.Code), Is.EqualTo(new[] { "First", "Second" }));
    }

    [Test, Description("The real seeded catalogue reads back whole, so a shape the seed produces but the read cannot serve is caught here.")]
    public async Task ShouldReturnTheWholeSeededCatalogue()
    {
        await UpsertAsync(ExperienceTypeSeedData.Build());

        Assert.That(await GetAsync(), Has.Count.EqualTo(ExperienceTypeSeedData.Build().Count));
    }

    private async Task<IReadOnlyCollection<ExperienceTypeDetail>> GetAsync()
    {
        ServiceResult<IReadOnlyCollection<ExperienceTypeDetail>> result = await _handler.Handle(new GetExperienceTypesRequest(), 
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private async Task UpsertAsync(IReadOnlyCollection<UpsertExperienceTypeDto> experienceTypes)
    {
        var handler = new UpsertExperienceTypesHandler(new NullLogger<UpsertExperienceTypesHandler>(), _context);

        ServiceResultBase result = await handler.Handle(new UpsertExperienceTypesRequest(experienceTypes), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }
}
