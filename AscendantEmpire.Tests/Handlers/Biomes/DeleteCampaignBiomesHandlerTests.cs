using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.Biomes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Handlers.Biomes;
internal class DeleteCampaignBiomesHandlerTests
{
    private AscendantEmpireContext _context = null!;
    private DeleteCampaignMapBiomeHandler _deleteHandler = null!;

    [SetUp]
    public async Task Setup()
    {
        string guid = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(databaseName: guid)
            .Options;

        _context = new AscendantEmpireContext(options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _deleteHandler = new DeleteCampaignMapBiomeHandler(new NullLogger<DeleteCampaignMapBiomeHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Should delete a Biome from the db.")]
    public async Task ShouldDeleteBiomeAsync()
    {
        _context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Arid, Name = "Arid", MapEditorHtmlColor = "#c0c0c0" });
        await _context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.CampaignBiomes.Count(), Is.GreaterThan(0), "No biomes were added to the context.");
        }

        // Do the delete
        ServiceResultBase deleteResult = await _deleteHandler.Handle(new DeleteCampaignMapBiomeRequest((int)BiomeType.Arid), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleteResult.HasSucceeded, Is.True, "Biome deletion failed, when it should have succeeded.");
            Assert.That(_context.CampaignBiomes.Count(), Is.EqualTo(0), "No biomes were deleted to the context, when they should be.");
        }
    }

    [Test, Description("Should fail if the Biome Type Id is not found.")]
    public async Task ShouldFailWhenBiomeNotFoundAsync()
    {
        _context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Arid, Name = "Arid", MapEditorHtmlColor = "#c0c0c0" });
        await _context.SaveChangesAsync();

        ServiceResultBase result = await _deleteHandler.Handle(new DeleteCampaignMapBiomeRequest(BiomeTypeId: (int)BiomeType.Ocean), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded, when it wasn't expected to.");
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound), "Should have been Not Found.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages when duplicate biome exists.");
        }
    }

    [Test, Description("Should fail when not a valid Biome Type Id")]
    public async Task ShouldFailWhenNotValidBiomeTypeAsync()
    {
        _context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Arid, Name = "Arid", MapEditorHtmlColor = "#c0c0c0" });
        await _context.SaveChangesAsync();

        ServiceResultBase result = await _deleteHandler.Handle(new DeleteCampaignMapBiomeRequest(BiomeTypeId: 200), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded, when it wasn't expected to.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages when duplicate biome exists.");
        }
    }
}