using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.BackendContracts.Technologies.GetExperienceTypes;
using AscendantEmpire.BackendContracts.Technologies.GetTechnologies;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests what the catalogue reads return, with the emphasis on grant targets being resolved to names the client could not resolve itself.
/// </summary>
/// <remarks>
/// In-memory: both handlers are LINQ projections over the context. Writes go through the real upsert handlers rather than being hand-built, so a change to
/// how the catalogue is stored shows up here rather than being papered over by a fixture that agrees with the old shape.
/// </remarks>
[TestFixture]
internal class GetTechnologiesHandlerTests : InMemoryTestBase
{
    private AscendantEmpireContext _context = null!;
    private GetTechnologiesHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new GetTechnologiesHandler(DbFactory);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    [Test, Description("An empty catalogue returns an empty collection rather than failing, so a client can call this before anything is seeded.")]
    public async Task ShouldReturnAnEmptyCollectionWhenNothingIsSeeded()
    {
        IReadOnlyCollection<CatalogueTechnology> catalogue = await GetAsync();

        Assert.That(catalogue, Is.Empty);
    }

    [Test, Description("A technology comes back with its own fields, so the editor has everything it needs to render a row without a second call.")]
    public async Task ShouldReturnATechnologysOwnFields()
    {
        await UpsertTechnologiesAsync(
        [
            new UpsertTechnologyDto
            {
                Code = "Stone.Foraging",
                DisplayName = "Foraging",
                Description = "Knowing which plants feed a people.",
                IconPath = "/technology/foraging.png",
                Era = TechnologyEra.Stone,
                SortOrder = 30,
                TreeColumn = 2,
                TreeRow = 5,
                DirectedRequired = 100,
                DiffusionRequired = 40
            }
        ]);

        CatalogueTechnology technology = (await GetAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(technology.Code, Is.EqualTo("Stone.Foraging"));
            Assert.That(technology.DisplayName, Is.EqualTo("Foraging"));
            Assert.That(technology.Description, Is.EqualTo("Knowing which plants feed a people."));
            Assert.That(technology.IconPath, Is.EqualTo("/technology/foraging.png"));
            Assert.That(technology.Era, Is.EqualTo(TechnologyEra.Stone));
            Assert.That(technology.SortOrder, Is.EqualTo(30));
            Assert.That(technology.TreeColumn, Is.EqualTo(2));
            Assert.That(technology.TreeRow, Is.EqualTo(5));
            Assert.That(technology.DirectedRequired, Is.EqualTo(100));
            Assert.That(technology.DiffusionRequired, Is.EqualTo(40));

            // Null rather than zero: the bucket is unused, not free.
            Assert.That(technology.ExperienceRequired, Is.Null);
        });
    }

    [Test, Description("Technologies come back in era and then sort order, so a client can render the list without re-sorting it.")]
    public async Task ShouldOrderByEraThenSortOrder()
    {
        await UpsertTechnologiesAsync(
        [
            Technology("Bronze.Smelting", "Smelting") with { Era = TechnologyEra.Bronze, SortOrder = 10 },
            Technology("Stone.Second", "Second") with { SortOrder = 20 },
            Technology("Stone.First", "First") with { SortOrder = 10 }
        ]);

        Assert.That(
            (await GetAsync()).Select(technology => technology.Code),
            Is.EqualTo(new[] { "Stone.First", "Stone.Second", "Bronze.Smelting" }));
    }

    [Test, Description("A prerequisite carries the code, name and kind of what it points at, so the tree can be drawn and labelled from one response.")]
    public async Task ShouldReturnPrerequisitesWithTheirNamesAndKind()
    {
        await UpsertTechnologiesAsync(
        [
            Technology("Stone.Foraging", "Foraging"),
            Technology("Stone.Woodcutting", "Woodcutting"),
            Technology("Stone.Hunting", "Hunting") with
            {
                Prerequisites =
                [
                    new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = "Stone.Foraging" },
                    new UpsertTechnologyPrerequisiteDto
                    {
                        PrerequisiteTechnologyCode = "Stone.Woodcutting",
                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional
                    }
                ]
            }
        ]);

        CatalogueTechnology hunting = (await GetAsync()).Single(technology => technology.Code == "Stone.Hunting");

        Assert.Multiple(() =>
        {
            // Ordered by the prerequisite's display name, so "Foraging" precedes "Woodcutting".
            Assert.That(
                hunting.Prerequisites.Select(prerequisite => prerequisite.PrerequisiteTechnologyDisplayName),
                Is.EqualTo(new[] { "Foraging", "Woodcutting" }));

            Assert.That(hunting.Prerequisites.First().PrerequisiteKind, Is.EqualTo(TechnologyPrerequisiteKind.Mandatory));
            Assert.That(hunting.Prerequisites.Last().PrerequisiteKind, Is.EqualTo(TechnologyPrerequisiteKind.Optional));
            Assert.That(hunting.Prerequisites.First().PrerequisiteTechnologyCode, Is.EqualTo("Stone.Foraging"));
        });
    }

    [Test, Description("An acquisition cost carries the Asset's code and name, which a client could not resolve without fetching the Asset catalogue too.")]
    public async Task ShouldResolveAcquisitionCostAssetNames()
    {
        await AddAssetAsync("ConstructionMaterials.Stone.Bricks.SandstoneBlock", "Sandstone Block");

        await UpsertTechnologiesAsync(
        [
            Technology("Stone.Masonry", "Masonry") with
            {
                AcquisitionCosts =
                [
                    new UpsertTechnologyAcquisitionCostDto
                    {
                        AssetCode = "ConstructionMaterials.Stone.Bricks.SandstoneBlock",
                        Quantity = 25
                    }
                ]
            }
        ]);

        CatalogueAcquisitionCost cost = (await GetAsync()).Single().AcquisitionCosts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(cost.AssetCode, Is.EqualTo("ConstructionMaterials.Stone.Bricks.SandstoneBlock"));
            Assert.That(cost.AssetDisplayName, Is.EqualTo("Sandstone Block"));
            Assert.That(cost.Quantity, Is.EqualTo(25));
        });
    }

    [Test, Description("A grant resolves its target's code and name from whichever catalogue its type names, since TargetId carries no foreign key.")]
    public async Task ShouldResolveGrantTargetNames()
    {
        int assetId = await AddAssetAsync("ConstructionMaterials.Wood.Logs.OakLog", "Oak Log");

        await UpsertTechnologiesAsync(
        [
            Technology("Stone.Woodcutting", "Woodcutting") with
            {
                Grants =
                [
                    new UpsertTechnologyGrantDto
                    {
                        TargetType = TechnologyGrantTargetType.Asset,
                        TargetCode = "ConstructionMaterials.Wood.Logs.OakLog"
                    }
                ]
            }
        ]);

        CatalogueGrant grant = (await GetAsync()).Single().Grants.Single();

        Assert.Multiple(() =>
        {
            Assert.That(grant.TargetType, Is.EqualTo(TechnologyGrantTargetType.Asset));
            Assert.That(grant.TargetId, Is.EqualTo(assetId));
            Assert.That(grant.TargetCode, Is.EqualTo("ConstructionMaterials.Wood.Logs.OakLog"));
            Assert.That(grant.TargetDisplayName, Is.EqualTo("Oak Log"));
            Assert.That(grant.GrantEffect, Is.EqualTo(TechnologyGrantEffect.Unlock));
        });
    }

    [Test, Description("A grant whose target has since been deleted comes back with null names rather than being hidden, so the editor can show it is broken.")]
    public async Task ShouldReturnNullNamesForADanglingGrantRatherThanHidingIt()
    {
        await AddAssetAsync("ConstructionMaterials.Wood.Logs.OakLog", "Oak Log");

        await UpsertTechnologiesAsync(
        [
            Technology("Stone.Woodcutting", "Woodcutting") with
            {
                Grants =
                [
                    new UpsertTechnologyGrantDto
                    {
                        TargetType = TechnologyGrantTargetType.Asset,
                        TargetCode = "ConstructionMaterials.Wood.Logs.OakLog"
                    }
                ]
            }
        ]);

        // The grant carries no foreign key, so nothing stops the Asset going away underneath it.
        _context.Assets.RemoveRange(_context.Assets);
        await _context.SaveChangesAsync();

        CatalogueGrant grant = (await GetAsync()).Single().Grants.Single();

        Assert.Multiple(() =>
        {
            Assert.That(grant.TargetCode, Is.Null);
            Assert.That(grant.TargetDisplayName, Is.Null);
            Assert.That(grant.TargetId, Is.GreaterThan(0), "the id is still reported, so the row can be found and fixed");
        });
    }

    [Test, Description("An experience source carries its type's code and name, so the tree can label where a technology's experience comes from.")]
    public async Task ShouldResolveExperienceSourceNames()
    {
        await UpsertExperienceTypesAsync([new UpsertExperienceTypeDto { Code = "Extraction.Foraging", DisplayName = "Foraging" }]);

        await UpsertTechnologiesAsync(
        [
            Technology("Stone.Foraging", "Foraging") with
            {
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Extraction.Foraging"]
            }
        ]);

        CatalogueExperienceSource source = (await GetAsync()).Single().ExperienceSources.Single();

        Assert.Multiple(() =>
        {
            Assert.That(source.ExperienceTypeCode, Is.EqualTo("Extraction.Foraging"));
            Assert.That(source.ExperienceTypeDisplayName, Is.EqualTo("Foraging"));
        });
    }

    [Test, Description("Empty child collections come back empty rather than null, so a client can iterate without a null check.")]
    public async Task ShouldReturnEmptyChildCollectionsRatherThanNull()
    {
        await UpsertTechnologiesAsync([Technology("Stone.Foraging", "Foraging")]);

        CatalogueTechnology technology = (await GetAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(technology.AcquisitionCosts, Is.Empty);
            Assert.That(technology.Prerequisites, Is.Empty);
            Assert.That(technology.Grants, Is.Empty);
            Assert.That(technology.ExperienceSources, Is.Empty);
        });
    }

    [Test, Description("The real seeded catalogue reads back whole, so a shape the seed produces but the read cannot serve is caught here.")]
    public async Task ShouldReturnTheWholeSeededCatalogue()
    {
        await UpsertExperienceTypesAsync(ExperienceTypeSeedData.Build());
        await UpsertTechnologiesAsync(TechnologySeedData.Build());

        IReadOnlyCollection<CatalogueTechnology> catalogue = await GetAsync();

        // Nothing below pins a figure that moves when content is authored, so adding a technology never sends anyone back here to re-count it.
        HashSet<string> servedCodes = catalogue.Select(technology => technology.Code).ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(catalogue, Is.Not.Empty);

            // A blank code or display name is a row the tree viewer cannot label and a prerequisite cannot name, so it is a shape the read should never serve.
            Assert.That(
                catalogue.Where(technology => string.IsNullOrWhiteSpace(technology.Code) || string.IsNullOrWhiteSpace(technology.DisplayName))
                    .Select(technology => $"#{technology.Id} '{technology.Code}' / '{technology.DisplayName}'"),
                Is.Empty);

            // Every edge has to land on a technology this same read served, or the client is handed a prerequisite with nothing to draw the edge to.
            Assert.That(
                catalogue.SelectMany(technology => technology.Prerequisites, (technology, prerequisite) => new { Technology = technology, Prerequisite = prerequisite })
                    .Where(edge => !servedCodes.Contains(edge.Prerequisite.PrerequisiteTechnologyCode))
                    .Select(edge => $"{edge.Technology.Code} -> {edge.Prerequisite.PrerequisiteTechnologyCode}"),
                Is.Empty);

            // Every technology uses at least one bucket, which the catalogue's own check constraint also insists on.
            Assert.That(
                catalogue.All(technology => technology.DirectedRequired is not null
                    || technology.ExperienceRequired is not null
                    || technology.DiffusionRequired is not null),
                Is.True);
        });
    }

    // --- Helpers ---------------------------------------------------------------------

    private static UpsertTechnologyDto Technology(string code, string displayName)
    {
        return new UpsertTechnologyDto
        {
            Code = code,
            DisplayName = displayName,
            Era = TechnologyEra.Stone,
            DirectedRequired = 100
        };
    }

    private async Task<IReadOnlyCollection<CatalogueTechnology>> GetAsync()
    {
        ServiceResult<IReadOnlyCollection<CatalogueTechnology>> result =
            await _handler.Handle(new GetTechnologiesRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private async Task<int> AddAssetAsync(string code, string displayName)
    {
        var metaCategory = new AssetMetaCategory
        {
            Code = "Probe",
            DisplayName = "Probe",
            SortOrder = 10,
            IconPath = string.Empty
        };

        var category = new AssetCategory
        {
            Code = "Probe.Category",
            DisplayName = "Probe Category",
            SortOrder = 10,
            CategoryType = AssetCategoryType.StandaloneAssets,
            MetaCategory = metaCategory
        };

        var asset = new Asset
        {
            Code = code,
            DisplayName = displayName,
            IconPath = string.Empty,
            IsGradedAsset = false,
            SortOrder = 10,
            MetaCategory = metaCategory,
            Category = category,
            StorageClass = new StorageClass { Code = $"{code}.Class", DisplayName = $"{displayName} Class" }
        };

        _context.Assets.Add(asset);
        await _context.SaveChangesAsync();

        return asset.Id;
    }

    private async Task UpsertExperienceTypesAsync(IReadOnlyCollection<UpsertExperienceTypeDto> experienceTypes)
    {
        var handler = new UpsertExperienceTypesHandler(new NullLogger<UpsertExperienceTypesHandler>(), _context);

        ServiceResultBase result = await handler.Handle(new UpsertExperienceTypesRequest(experienceTypes), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }

    private async Task UpsertTechnologiesAsync(IReadOnlyCollection<UpsertTechnologyDto> technologies)
    {
        var handler = new UpsertTechnologiesHandler(new NullLogger<UpsertTechnologiesHandler>(), _context);

        ServiceResultBase result = await handler.Handle(new UpsertTechnologiesRequest(technologies), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }
}
