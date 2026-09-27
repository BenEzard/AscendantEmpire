using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Resources.Handlers;
using AscendantEmpire.BackendContracts.Resources;
using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Enums.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Resources;

/// <summary>
/// Tests how the resource upsert matches existing rows and what it writes, with the emphasis on codes being the match key.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the handler sticks to LINQ, <c>Add</c> and <c>SaveChanges</c>, and every rule exercised here is enforced in the handler rather than by a database
/// constraint, so no container is needed. The unique indexes on <c>Code</c> are deliberately not exercised — in-memory cannot reproduce them.
/// </para>
/// <para>
/// The rename tests are the important ones. Codes exist so a seed can be re-run after an edit and update its rows instead of inserting a second set, and that
/// only holds while a supplied code is honoured at every level rather than being re-derived from the display name.
/// </para>
/// </remarks>
[TestFixture]
internal class UpsertResourcesHandlerTests : InMemoryTestBase
{
    private AscendantEmpireContext _context = null!;
    private UpsertResourcesHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new UpsertResourcesHandler(new NullLogger<UpsertResourcesHandler>(), _context);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    [Test, Description("A supplied code is stored verbatim at every level, rather than being re-derived or prefixed with its parent's code.")]
    public async Task ShouldStoreSuppliedCodesVerbatimAtEveryLevel()
    {
        await UpsertAsync(BuildCatalogue());

        Assert.Multiple(() =>
        {
            Assert.That(_context.ResourceMetaCategories.Single().Code, Is.EqualTo("ConstructionMaterials"));
            Assert.That(_context.ResourceCategories.Single(c => c.DisplayName == "Wood").Code, Is.EqualTo("ConstructionMaterials.Wood"));
            Assert.That(_context.ResourceGroups.Single().Code, Is.EqualTo("ConstructionMaterials.Wood.Logs"));
            Assert.That(_context.Resources.Single(r => r.DisplayName == "Oak Log").Code, Is.EqualTo("ConstructionMaterials.Wood.Logs.OakLog"));
        });
    }

    [Test, Description("A code left unset is still derived from the parent's code and the display name, so existing callers keep working unchanged.")]
    public async Task ShouldDeriveCodesThatAreNotSupplied()
    {
        await UpsertAsync(
        [
            new UpsertResourceMetaCategoryDto
            {
                DisplayName = "Construction Materials",
                Categories =
                [
                    new UpsertResourceCategoryDto
                    {
                        DisplayName = "Wood",
                        CategoryType = ResourceCategoryType.StandaloneResources,
                        Resources = [new UpsertResourceDto { DisplayName = "Oak Log" }]
                    }
                ]
            }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(_context.ResourceMetaCategories.Single().Code, Is.EqualTo("ConstructionMaterials"));
            Assert.That(_context.ResourceCategories.Single().Code, Is.EqualTo("ConstructionMaterials.Wood"));
            Assert.That(_context.Resources.Single().Code, Is.EqualTo("ConstructionMaterials.Wood.OakLog"));
        });
    }

    [Test, Description("Re-running an unchanged catalogue updates the same rows rather than inserting a second set, which is what makes re-seeding safe.")]
    public async Task ShouldNotDuplicateRowsWhenRerunUnchanged()
    {
        await UpsertAsync(BuildCatalogue());
        await UpsertAsync(BuildCatalogue());

        Assert.Multiple(() =>
        {
            Assert.That(_context.ResourceMetaCategories.Count(), Is.EqualTo(1));
            Assert.That(_context.ResourceCategories.Count(), Is.EqualTo(1));
            Assert.That(_context.ResourceGroups.Count(), Is.EqualTo(1));
            Assert.That(_context.Resources.Count(), Is.EqualTo(2));
        });
    }

    [Test, Description("Renaming a display name while keeping the code renames the existing rows at every level instead of orphaning them behind new ones.")]
    public async Task ShouldRenameRatherThanDuplicateWhenOnlyTheDisplayNameChanges()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(
            metaDisplayName: "Building Materials",
            categoryDisplayName: "Timber",
            groupDisplayName: "Trunks"));

        Assert.Multiple(() =>
        {
            Assert.That(_context.ResourceMetaCategories.Count(), Is.EqualTo(1), "The meta-category was duplicated rather than renamed.");
            Assert.That(_context.ResourceMetaCategories.Single().DisplayName, Is.EqualTo("Building Materials"));

            Assert.That(_context.ResourceCategories.Count(), Is.EqualTo(1), "The category was duplicated rather than renamed.");
            Assert.That(_context.ResourceCategories.Single().DisplayName, Is.EqualTo("Timber"));

            Assert.That(_context.ResourceGroups.Count(), Is.EqualTo(1), "The group was duplicated rather than renamed.");
            Assert.That(_context.ResourceGroups.Single().DisplayName, Is.EqualTo("Trunks"));
        });
    }

    [Test, Description("Two rows resolving to the same code are refused before anything is written, so a collision cannot half-apply.")]
    public async Task ShouldRefuseACatalogueWithCollidingCodes()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertResourcesRequest(
            [
                new UpsertResourceMetaCategoryDto
                {
                    Code = "ConstructionMaterials",
                    DisplayName = "Construction Materials",
                    Categories =
                    [
                        new UpsertResourceCategoryDto
                        {
                            Code = "ConstructionMaterials.Wood",
                            DisplayName = "Wood",
                            CategoryType = ResourceCategoryType.StandaloneResources,
                            Resources =
                            [
                                new UpsertResourceDto { Code = "Duplicated", DisplayName = "Oak Log" },
                                new UpsertResourceDto { Code = "Duplicated", DisplayName = "Ash Log" }
                            ]
                        }
                    ]
                }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(_context.Resources.Count(), Is.Zero, "Rows were written despite the collision being refused.");
        });
    }

    [Test, Description("Resources are given sort orders spaced by ten from their position in the list, so file order is display order.")]
    public async Task ShouldDeriveResourceSortOrderFromListPosition()
    {
        await UpsertAsync(BuildCatalogue());

        Assert.Multiple(() =>
        {
            Assert.That(_context.Resources.Single(r => r.DisplayName == "Oak Log").SortOrder, Is.EqualTo(10));
            Assert.That(_context.Resources.Single(r => r.DisplayName == "Ash Log").SortOrder, Is.EqualTo(20));
        });
    }

    [Test, Description("A supplied resource sort order is kept, so an explicit ordering is not overwritten by list position.")]
    public async Task ShouldKeepASuppliedResourceSortOrder()
    {
        await UpsertAsync(BuildCatalogue(firstResourceSortOrder: 500));

        Assert.That(_context.Resources.Single(r => r.DisplayName == "Oak Log").SortOrder, Is.EqualTo(500));
    }

    [Test, Description("Each grade of a graded resource is stored as its own row under one display name, told apart by its code and grade.")]
    public async Task ShouldStoreOneRowPerGradeOfAGradedResource()
    {
        await UpsertAsync(
        [
            new UpsertResourceMetaCategoryDto
            {
                Code = "Weapons",
                DisplayName = "Weapons",
                Categories =
                [
                    new UpsertResourceCategoryDto
                    {
                        Code = "Weapons.MeleeWeapons",
                        DisplayName = "Melee Weapons",
                        CategoryType = ResourceCategoryType.StandaloneResources,
                        Resources =
                        [
                            GradedKnife(ResourceGrade.Low),
                            GradedKnife(ResourceGrade.Medium),
                            GradedKnife(ResourceGrade.High)
                        ]
                    }
                ]
            }
        ]);

        Resource[] knives = [.. _context.Resources.Where(r => r.DisplayName == "Bone Knife")];

        Assert.Multiple(() =>
        {
            Assert.That(knives, Has.Length.EqualTo(3));
            Assert.That(knives.Select(k => k.ResourceGrade), Is.EquivalentTo(new[] { ResourceGrade.Low, ResourceGrade.Medium, ResourceGrade.High }));
            Assert.That(knives.Select(k => k.Code), Is.Unique);
            Assert.That(knives.Select(k => k.IsGradedResource), Is.All.True);
        });
    }

    [Test, Description("An update that omits the description and icon leaves the stored ones alone, so a partial edit cannot blank flavour text.")]
    public async Task ShouldLeaveDescriptionAndIconAloneWhenAnUpdateOmitsThem()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(includeResourceDetail: false));

        Resource oakLog = _context.Resources.Single(r => r.DisplayName == "Oak Log");

        Assert.Multiple(() =>
        {
            Assert.That(oakLog.Description, Is.EqualTo("A dense hardwood."));
            Assert.That(oakLog.IconPath, Is.EqualTo("/resources/wood/oak-log_40px.png"));
        });
    }

    private async Task UpsertAsync(IReadOnlyCollection<UpsertResourceMetaCategoryDto> metaCategories)
    {
        ServiceResultBase result = await _handler.Handle(new UpsertResourcesRequest(metaCategories), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"Upsert failed: {string.Join("; ", result.Errors)}");

        // The handler shares this context, so its tracked entities would otherwise satisfy the next assertion without a round trip.
        _context.ChangeTracker.Clear();
    }

    /// <summary>
    /// One meta-category holding one grouped category with a group of two logs, which is enough shape to exercise all four levels at once.
    /// </summary>
    /// <param name="includeResourceDetail">False omits each resource's description and icon, to exercise the partial-update rule.</param>
    private static IReadOnlyCollection<UpsertResourceMetaCategoryDto> BuildCatalogue(
        string metaDisplayName = "Construction Materials",
        string categoryDisplayName = "Wood",
        string groupDisplayName = "Logs",
        int? firstResourceSortOrder = null,
        bool includeResourceDetail = true)
    {
        return
        [
            new UpsertResourceMetaCategoryDto
            {
                Code = "ConstructionMaterials",
                DisplayName = metaDisplayName,
                IconPath = "/resources/wood/oak-log_40px.png",
                Categories =
                [
                    new UpsertResourceCategoryDto
                    {
                        Code = "ConstructionMaterials.Wood",
                        DisplayName = categoryDisplayName,
                        CategoryType = ResourceCategoryType.GroupedResources,
                        Groups =
                        [
                            new UpsertResourceGroupDto
                            {
                                Code = "ConstructionMaterials.Wood.Logs",
                                DisplayName = groupDisplayName,
                                Resources =
                                [
                                    new UpsertResourceDto
                                    {
                                        Code = "ConstructionMaterials.Wood.Logs.OakLog",
                                        DisplayName = "Oak Log",
                                        IconPath = includeResourceDetail ? "/resources/wood/oak-log_40px.png" : null,
                                        Description = includeResourceDetail ? "A dense hardwood." : null,
                                        SortOrder = firstResourceSortOrder
                                    },
                                    new UpsertResourceDto
                                    {
                                        Code = "ConstructionMaterials.Wood.Logs.AshLog",
                                        DisplayName = "Ash Log"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        ];
    }

    private static UpsertResourceDto GradedKnife(ResourceGrade grade)
    {
        return new UpsertResourceDto
        {
            Code = $"Weapons.MeleeWeapons.BoneKnife.{grade}",
            DisplayName = "Bone Knife",
            IsGradedResource = true,
            ResourceGrade = grade
        };
    }
}
