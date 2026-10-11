using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests what the technology upsert accepts, what it rejects, and how it replaces a technology's child collections.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the handler sticks to LINQ, <c>Add</c> and <c>SaveChanges</c>. The check constraints are not exercised here — in-memory cannot reproduce them,
/// which is precisely why the handler duplicates each one as a message that names the offending technology.
/// </para>
/// <para>
/// The cycle tests are the important ones. A cycle cannot be expressed as a database constraint, it makes every technology in it permanently unreachable, and
/// it would stop the availability reconcile from ever settling — so the handler is the only thing standing between a bad seed and an unplayable game.
/// </para>
/// </remarks>
[TestFixture]
internal class UpsertTechnologiesHandlerTests : InMemoryTestBase
{
    private AscendantEmpireContext _context = null!;
    private UpsertTechnologiesHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new UpsertTechnologiesHandler(new NullLogger<UpsertTechnologiesHandler>(), _context);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    // --- Writing ---------------------------------------------------------------------

    [Test, Description("Codes are stored verbatim rather than derived, because prerequisites reference them and a reworded name must not re-key the row.")]
    public async Task ShouldStoreSuppliedCodesVerbatim()
    {
        await UpsertAsync([Technology("Stone.Foraging", "Foraging")]);

        Assert.That(_context.Technologies.Single().Code, Is.EqualTo("Stone.Foraging"));
    }

    [Test, Description("Sort order is derived within the era, not across the request, because the uniqueness constraint is on (Era, SortOrder).")]
    public async Task ShouldDeriveSortOrderWithinEachEraSeparately()
    {
        await UpsertAsync(
        [
            Technology("Stone.Foraging", "Foraging"),
            Technology("Stone.Hunting", "Hunting"),
            Technology("Bronze.Smelting", "Smelting") with { Era = TechnologyEra.Bronze }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(_context.Technologies.Single(t => t.Code == "Stone.Foraging").SortOrder, Is.EqualTo(10));
            Assert.That(_context.Technologies.Single(t => t.Code == "Stone.Hunting").SortOrder, Is.EqualTo(20));

            // Restarts at 10 rather than continuing to 30, because it is the first of its era.
            Assert.That(_context.Technologies.Single(t => t.Code == "Bronze.Smelting").SortOrder, Is.EqualTo(10));
        });
    }

    [Test, Description("Re-running with a changed display name renames the existing row rather than inserting a second one, which is what makes reseeding safe.")]
    public async Task ShouldRenameRatherThanDuplicateWhenTheCodeIsUnchanged()
    {
        await UpsertAsync([Technology("Stone.Foraging", "Foraging")]);
        await UpsertAsync([Technology("Stone.Foraging", "Gathering")]);

        Assert.Multiple(() =>
        {
            Assert.That(_context.Technologies.Count(), Is.EqualTo(1));
            Assert.That(_context.Technologies.Single().DisplayName, Is.EqualTo("Gathering"));
        });
    }

    [Test, Description("Bucket requirements are overwritten even when null, so clearing a bucket through the editor actually clears it.")]
    public async Task ShouldClearABucketThatIsNoLongerRequired()
    {
        await UpsertAsync(
        [
            Technology("Stone.Foraging", "Foraging") with
            {
                DirectedRequired = 100,
                DiffusionRequired = 50
            }
        ]);

        await UpsertAsync([Technology("Stone.Foraging", "Foraging") with { DirectedRequired = 100 }]);

        Assert.That(_context.Technologies.Single().DiffusionRequired, Is.Null);
    }

    [Test, Description("A prerequisite naming a technology defined later in the same request resolves, so one batch can carry a whole tree.")]
    public async Task ShouldResolveAPrerequisiteDefinedLaterInTheSameRequest()
    {
        await UpsertAsync(
        [
            Technology("Stone.Hunting", "Hunting") with { Prerequisites = [Prerequisite("Stone.Foraging")] },
            Technology("Stone.Foraging", "Foraging")
        ]);

        TechnologyPrerequisite edge = _context.TechnologyPrerequisites
            .Include(prerequisite => prerequisite.Technology)
            .Include(prerequisite => prerequisite.PrerequisiteTechnology)
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(edge.Technology.Code, Is.EqualTo("Stone.Hunting"));
            Assert.That(edge.PrerequisiteTechnology.Code, Is.EqualTo("Stone.Foraging"));
            Assert.That(edge.PrerequisiteKind, Is.EqualTo(TechnologyPrerequisiteKind.Mandatory));
        });
    }

    [Test, Description("A prerequisite naming a technology already in the database resolves, so the editor can edit one technology at a time.")]
    public async Task ShouldResolveAPrerequisiteAlreadyInTheDatabase()
    {
        await UpsertAsync([Technology("Stone.Foraging", "Foraging")]);
        await UpsertAsync([Technology("Stone.Hunting", "Hunting") with { Prerequisites = [Prerequisite("Stone.Foraging")] }]);

        Assert.That(_context.TechnologyPrerequisites.Count(), Is.EqualTo(1));
    }

    [Test, Description("Child collections are replaced wholesale, so an edge dropped from the request is removed rather than left behind.")]
    public async Task ShouldReplaceChildCollectionsRatherThanMergeThem()
    {
        await UpsertAsync(
        [
            Technology("Stone.Foraging", "Foraging"),
            Technology("Stone.Woodcutting", "Woodcutting"),
            Technology("Stone.Hunting", "Hunting") with
            {
                Prerequisites = [Prerequisite("Stone.Foraging"), Prerequisite("Stone.Woodcutting")]
            }
        ]);

        await UpsertAsync([Technology("Stone.Hunting", "Hunting") with { Prerequisites = [Prerequisite("Stone.Foraging")] }]);

        TechnologyPrerequisite remaining = _context.TechnologyPrerequisites
            .Include(prerequisite => prerequisite.PrerequisiteTechnology)
            .Single();

        Assert.That(remaining.PrerequisiteTechnology.Code, Is.EqualTo("Stone.Foraging"));
    }

    [Test, Description("An existing edge whose kind changed is updated in place rather than deleted and reinserted, which the keyed upsert exists to avoid.")]
    public async Task ShouldUpdateThePrerequisiteKindInPlace()
    {
        await UpsertAsync(
        [
            Technology("Stone.Foraging", "Foraging"),
            Technology("Stone.Hunting", "Hunting") with { Prerequisites = [Prerequisite("Stone.Foraging")] }
        ]);

        await UpsertAsync(
        [
            Technology("Stone.Hunting", "Hunting") with
            {
                Prerequisites = [Prerequisite("Stone.Foraging", TechnologyPrerequisiteKind.Optional)]
            }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(_context.TechnologyPrerequisites.Count(), Is.EqualTo(1));
            Assert.That(_context.TechnologyPrerequisites.Single().PrerequisiteKind, Is.EqualTo(TechnologyPrerequisiteKind.Optional));
        });
    }

    [Test, Description("An acquisition cost resolves its Asset by code and stores the quantity, since the seed is authored before any id exists.")]
    public async Task ShouldResolveAcquisitionCostsByAssetCode()
    {
        int assetId = await AddAssetAsync("ConstructionMaterials.Stone.Bricks.SandstoneBlock");

        await UpsertAsync(
        [
            Technology("Stone.Masonry", "Masonry") with
            {
                AcquisitionCosts =
                [
                    new UpsertTechnologyAcquisitionCostDto { AssetCode = "ConstructionMaterials.Stone.Bricks.SandstoneBlock", Quantity = 25 }
                ]
            }
        ]);

        TechnologyAcquisitionCost cost = _context.TechnologyAcquisitionCosts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(cost.AssetId, Is.EqualTo(assetId));
            Assert.That(cost.Quantity, Is.EqualTo(25));
        });
    }

    [Test, Description("A grant resolves its target by code against the catalogue its type names, which is the only guard an unconstrained TargetId gets.")]
    public async Task ShouldResolveAGrantTargetByCodeForItsType()
    {
        int assetId = await AddAssetAsync("ConstructionMaterials.Wood.Logs.OakLog");

        await UpsertAsync(
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

        TechnologyGrant grant = _context.TechnologyGrants.Single();

        Assert.Multiple(() =>
        {
            Assert.That(grant.TargetType, Is.EqualTo(TechnologyGrantTargetType.Asset));
            Assert.That(grant.TargetId, Is.EqualTo(assetId));
            Assert.That(grant.GrantEffect, Is.EqualTo(TechnologyGrantEffect.Unlock));
        });
    }

    [Test, Description("An experience mapping resolves its type by code, so a technology's experience sources can be authored alongside it.")]
    public async Task ShouldResolveExperienceTypesByCode()
    {
        int experienceTypeId = await AddExperienceTypeAsync("Extraction.Foraging");

        await UpsertAsync(
        [
            Technology("Stone.Foraging", "Foraging") with
            {
                ExperienceRequired = 50,
                ExperienceTypeCodes = ["Extraction.Foraging"]
            }
        ]);

        Assert.That(_context.TechnologyExperienceTypes.Single().ExperienceTypeId, Is.EqualTo(experienceTypeId));
    }

    // --- Cycles ----------------------------------------------------------------------

    [Test, Description("A two-technology cycle inside one request is rejected, since the database cannot express acyclicity and both would be unreachable.")]
    public async Task ShouldRejectATwoTechnologyCycleWithinOneRequest()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Alpha", "Alpha") with { Prerequisites = [Prerequisite("Stone.Beta")] },
                Technology("Stone.Beta", "Beta") with { Prerequisites = [Prerequisite("Stone.Alpha")] }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("cycle"));
        });
    }

    [Test, Description("A longer cycle is rejected too, so the check is a real graph walk rather than a pairwise comparison.")]
    public async Task ShouldRejectAThreeTechnologyCycle()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Alpha", "Alpha") with { Prerequisites = [Prerequisite("Stone.Gamma")] },
                Technology("Stone.Beta", "Beta") with { Prerequisites = [Prerequisite("Stone.Alpha")] },
                Technology("Stone.Gamma", "Gamma") with { Prerequisites = [Prerequisite("Stone.Beta")] }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("cycle"));
        });
    }

    [Test, Description("A cycle closed against an edge already in the database is rejected, so editing one technology cannot break the whole tree.")]
    public async Task ShouldRejectACycleClosedAgainstStoredEdges()
    {
        await UpsertAsync(
        [
            Technology("Stone.Alpha", "Alpha"),
            Technology("Stone.Beta", "Beta") with { Prerequisites = [Prerequisite("Stone.Alpha")] }
        ]);

        // Alpha now requires Beta, which already requires Alpha. The offending edge is in the request; the one that closes the loop is not.
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest([Technology("Stone.Alpha", "Alpha") with { Prerequisites = [Prerequisite("Stone.Beta")] }]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("cycle"));
        });
    }

    [Test, Description("Clearing the edge that would have closed a loop is accepted, because the graph is checked as it will be, not as it was.")]
    public async Task ShouldAcceptAnEdgeWhoseOppositeIsBeingRemovedBySameRequest()
    {
        await UpsertAsync(
        [
            Technology("Stone.Alpha", "Alpha"),
            Technology("Stone.Beta", "Beta") with { Prerequisites = [Prerequisite("Stone.Alpha")] }
        ]);

        // Beta's edge is replaced with nothing in the same request that gives Alpha an edge to Beta, so no cycle ever exists.
        await UpsertAsync(
        [
            Technology("Stone.Beta", "Beta"),
            Technology("Stone.Alpha", "Alpha") with { Prerequisites = [Prerequisite("Stone.Beta")] }
        ]);

        Assert.That(_context.TechnologyPrerequisites.Count(), Is.EqualTo(1));
    }

    [Test, Description("A diamond is accepted, so a merging tree is not mistaken for a cycle.")]
    public async Task ShouldAcceptADiamondShapedTree()
    {
        await UpsertAsync(
        [
            Technology("Stone.Root", "Root"),
            Technology("Stone.Left", "Left") with { Prerequisites = [Prerequisite("Stone.Root")] },
            Technology("Stone.Right", "Right") with { Prerequisites = [Prerequisite("Stone.Root")] },
            Technology("Stone.Join", "Join") with { Prerequisites = [Prerequisite("Stone.Left"), Prerequisite("Stone.Right")] }
        ]);

        Assert.That(_context.TechnologyPrerequisites.Count(), Is.EqualTo(4));
    }

    [Test, Description("A technology naming itself as a prerequisite is rejected by name, rather than surfacing as the check constraint it mirrors.")]
    public async Task ShouldRejectASelfPrerequisite()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest([Technology("Stone.Alpha", "Alpha") with { Prerequisites = [Prerequisite("Stone.Alpha")] }]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("own prerequisite"));
        });
    }

    // --- Rejections ------------------------------------------------------------------

    [Test, Description("An empty request fails rather than silently succeeding, so a misconfigured seed step is reported instead of looking like a no-op.")]
    public async Task ShouldFailWhenNoTechnologiesAreSupplied()
    {
        ServiceResultBase result = await _handler.Handle(new UpsertTechnologiesRequest([]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("A technology requiring no bucket at all is rejected by name, mirroring CK_Technology_AtLeastOneBucket.")]
    public async Task ShouldRejectATechnologyWithNoBucketRequirement()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest([Technology("Stone.Alpha", "Alpha") with { DirectedRequired = null }]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("at least one research bucket"));
        });
    }

    [Test, Description("An experience requirement with no experience types is rejected, because the bucket could never be filled.")]
    public async Task ShouldRejectAnExperienceRequirementWithNoSources()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest([Technology("Stone.Alpha", "Alpha") with { ExperienceRequired = 50 }]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("no experience types"));
        });
    }

    [Test, Description("Experience types on a technology with no experience bucket are rejected, because the points would have nowhere to go.")]
    public async Task ShouldRejectExperienceTypesWithNoExperienceRequirement()
    {
        await AddExperienceTypeAsync("Extraction.Foraging");

        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest([Technology("Stone.Alpha", "Alpha") with { ExperienceTypeCodes = ["Extraction.Foraging"] }]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("no experience requirement"));
        });
    }

    [Test, Description("An unresolvable prerequisite code is rejected and named, rather than silently dropping the edge.")]
    public async Task ShouldRejectAnUnknownPrerequisiteCode()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest([Technology("Stone.Alpha", "Alpha") with { Prerequisites = [Prerequisite("Stone.Missing")] }]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Stone.Missing"));
        });
    }

    [Test, Description("An unresolvable grant target is rejected and named, which matters because TargetId carries no foreign key to catch it later.")]
    public async Task ShouldRejectAnUnknownGrantTarget()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Alpha", "Alpha") with
                {
                    Grants = [new UpsertTechnologyGrantDto { TargetType = TechnologyGrantTargetType.Asset, TargetCode = "Nope.Missing" }]
                }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Nope.Missing"));
        });
    }

    [Test, Description("A grant naming a code that exists in a different catalogue is rejected, so the target type is actually honoured.")]
    public async Task ShouldRejectAGrantWhoseCodeBelongsToAnotherCatalogue()
    {
        await AddAssetAsync("ConstructionMaterials.Wood.Logs.OakLog");

        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Alpha", "Alpha") with
                {
                    Grants =
                    [
                        new UpsertTechnologyGrantDto
                        {
                            // The code is a real Asset, but the type says to look in the unit-component catalogue.
                            TargetType = TechnologyGrantTargetType.UnitComponent,
                            TargetCode = "ConstructionMaterials.Wood.Logs.OakLog"
                        }
                    ]
                }
            ]),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("An unresolvable cost Asset is rejected, naming the Assets catalogue as the thing that has to be seeded first.")]
    public async Task ShouldRejectAnUnknownCostAssetCode()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Alpha", "Alpha") with
                {
                    AcquisitionCosts = [new UpsertTechnologyAcquisitionCostDto { AssetCode = "Nope.Missing", Quantity = 5 }]
                }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Nope.Missing"));
        });
    }

    [Test, Description("A zero or negative cost quantity is rejected, mirroring CK_TechnologyAcquisitionCost_QuantityPositive.")]
    public async Task ShouldRejectANonPositiveCostQuantity()
    {
        await AddAssetAsync("ConstructionMaterials.Wood.Logs.OakLog");

        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Alpha", "Alpha") with
                {
                    AcquisitionCosts =
                    [
                        new UpsertTechnologyAcquisitionCostDto { AssetCode = "ConstructionMaterials.Wood.Logs.OakLog", Quantity = 0 }
                    ]
                }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("greater than zero"));
        });
    }

    [Test, Description("Two entries sharing a code are rejected, since in-memory cannot reproduce the unique index that would otherwise catch it.")]
    public async Task ShouldRejectDuplicateCodesWithinOneRequest()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest([Technology("Stone.Alpha", "Alpha"), Technology("Stone.Alpha", "Also Alpha")]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Stone.Alpha"));
        });
    }

    [Test, Description("Two entries in one era sharing an explicit sort order are rejected, mirroring UX_Technology_Era_SortOrder.")]
    public async Task ShouldRejectDuplicateSortOrdersWithinOneEra()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Alpha", "Alpha") with { SortOrder = 10 },
                Technology("Stone.Beta", "Beta") with { SortOrder = 10 }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("sort order"));
        });
    }

    [Test, Description("The same sort order in two different eras is accepted, because uniqueness is scoped to the era.")]
    public async Task ShouldAcceptTheSameSortOrderInDifferentEras()
    {
        await UpsertAsync(
        [
            Technology("Stone.Alpha", "Alpha") with { SortOrder = 10 },
            Technology("Bronze.Beta", "Beta") with { Era = TechnologyEra.Bronze, SortOrder = 10 }
        ]);

        Assert.That(_context.Technologies.Count(), Is.EqualTo(2));
    }

    [Test, Description("The same prerequisite named twice is rejected by technology, rather than surfacing as a composite key violation.")]
    public async Task ShouldRejectTheSamePrerequisiteTwice()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(
            [
                Technology("Stone.Foraging", "Foraging"),
                Technology("Stone.Hunting", "Hunting") with
                {
                    Prerequisites = [Prerequisite("Stone.Foraging"), Prerequisite("Stone.Foraging", TechnologyPrerequisiteKind.Optional)]
                }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("same prerequisite"));
        });
    }

    // --- The real seed data ----------------------------------------------------------

    [Test, Description("The real seed data upserts cleanly - which also proves its prerequisite graph is acyclic, since the upsert rejects a cycle outright.")]
    public async Task ShouldUpsertTheRealSeedData()
    {
        await SeedExperienceTypesAsync();

        ServiceResultBase result = await _handler.Handle(
            new UpsertTechnologiesRequest(TechnologySeedData.Build()),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(_context.Technologies.Count(), Is.EqualTo(TechnologySeedData.Build().Count));
        });
    }

    [Test, Description("Re-running the real seed data changes no row counts, which is what makes the seed safe to run over an existing database.")]
    public async Task ShouldBeIdempotentOverTheRealSeedData()
    {
        await SeedExperienceTypesAsync();
        await UpsertAsync(TechnologySeedData.Build());

        int technologies = _context.Technologies.Count();
        int prerequisites = _context.TechnologyPrerequisites.Count();
        int experienceMappings = _context.TechnologyExperienceTypes.Count();

        await UpsertAsync(TechnologySeedData.Build());

        Assert.Multiple(() =>
        {
            Assert.That(_context.Technologies.Count(), Is.EqualTo(technologies));
            Assert.That(_context.TechnologyPrerequisites.Count(), Is.EqualTo(prerequisites));
            Assert.That(_context.TechnologyExperienceTypes.Count(), Is.EqualTo(experienceMappings));
        });
    }

    [Test, Description("The catalogue keeps its shape - 208 technologies, 255 edges, 21 of them optional - so a hand edit that drops rows or edges is noticed.")]
    public async Task ShouldSeedTheWholeCatalogue()
    {
        await SeedExperienceTypesAsync();
        await UpsertAsync(TechnologySeedData.Build());

        Assert.Multiple(() =>
        {
            Assert.That(_context.Technologies.Count(), Is.EqualTo(208));
            Assert.That(_context.TechnologyPrerequisites.Count(), Is.EqualTo(255));
            Assert.That(
                _context.TechnologyPrerequisites.Count(edge => edge.PrerequisiteKind == TechnologyPrerequisiteKind.Optional),
                Is.EqualTo(21));
            Assert.That(_context.TechnologyExperienceTypes.Select(mapping => mapping.TechnologyId).Distinct().Count(), Is.EqualTo(27));
        });
    }

    [Test, Description("Every seeded technology sits in the Stone era, since no era has been assigned to any of them yet.")]
    public async Task ShouldSeedEveryTechnologyInTheStoneEraUntilErasAreAssigned()
    {
        await SeedExperienceTypesAsync();
        await UpsertAsync(TechnologySeedData.Build());

        Assert.That(_context.Technologies.Select(technology => technology.Era).Distinct(), Is.EqualTo(new[] { TechnologyEra.Stone }));
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

    private static UpsertTechnologyPrerequisiteDto Prerequisite(
        string code,
        TechnologyPrerequisiteKind kind = TechnologyPrerequisiteKind.Mandatory)
    {
        return new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = code, PrerequisiteKind = kind };
    }

    private async Task<int> AddAssetAsync(string code)
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
            DisplayName = code,
            IconPath = string.Empty,
            IsGradedAsset = false,
            SortOrder = 10,
            MetaCategory = metaCategory,
            Category = category
        };

        _context.Assets.Add(asset);
        await _context.SaveChangesAsync();

        return asset.Id;
    }

    private async Task<int> AddExperienceTypeAsync(string code)
    {
        var experienceType = new ExperienceType
        {
            Code = code,
            DisplayName = code,
            IconPath = string.Empty,
            SortOrder = 10
        };

        _context.ExperienceTypes.Add(experienceType);
        await _context.SaveChangesAsync();

        return experienceType.Id;
    }

    /// <summary>
    /// Seeds the real experience type catalogue, which the real technology seed data names by code.
    /// </summary>
    private async Task SeedExperienceTypesAsync()
    {
        var handler = new UpsertExperienceTypesHandler(new NullLogger<UpsertExperienceTypesHandler>(), _context);

        ServiceResultBase result = await handler.Handle(
            new UpsertExperienceTypesRequest(ExperienceTypeSeedData.Build()),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }

    private async Task UpsertAsync(IReadOnlyCollection<UpsertTechnologyDto> technologies)
    {
        ServiceResultBase result = await _handler.Handle(new UpsertTechnologiesRequest(technologies), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }
}
