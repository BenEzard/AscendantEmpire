using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests how the asset upsert matches existing rows and what it writes, with the emphasis on codes being the match key.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the handler sticks to LINQ, <c>Add</c> and <c>SaveChanges</c>, and every rule exercised here is enforced in the handler rather than by a database
/// constraint, so no container is needed. The unique indexes on <c>Code</c> are deliberately not exercised — in-memory cannot reproduce them, and neither can
/// it reproduce their case-insensitivity, since the provider compares strings ordinally. Anything that turns on the real collation lives in
/// <see cref="UpsertAssetsCodeCollationTests"/> instead.
/// </para>
/// <para>
/// The rename tests are the important ones. Codes exist so a seed can be re-run after an edit and update its rows instead of inserting a second set, and that
/// only holds while a supplied code is honoured at every level rather than being re-derived from the display name.
/// </para>
/// </remarks>
[TestFixture]
internal class UpsertAssetsHandlerTests : InMemoryTestBase
{
    private AscendantEmpireContext _context = null!;
    private UpsertAssetsHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new UpsertAssetsHandler(new NullLogger<UpsertAssetsHandler>(), _context);
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
            Assert.That(_context.AssetMetaCategories.Single().Code, Is.EqualTo("ConstructionMaterials"));
            Assert.That(_context.AssetCategories.Single(c => c.DisplayName == "Wood").Code, Is.EqualTo("ConstructionMaterials.Wood"));
            Assert.That(_context.AssetGroups.Single().Code, Is.EqualTo("ConstructionMaterials.Wood.Logs"));
            Assert.That(_context.Assets.Single(r => r.DisplayName == "Oak Log").Code, Is.EqualTo("ConstructionMaterials.Wood.Logs.OakLog"));
        });
    }

    [Test, Description("A code left unset is still derived from the parent's code and the display name, so existing callers keep working unchanged.")]
    public async Task ShouldDeriveCodesThatAreNotSupplied()
    {
        await UpsertAsync(
        [
            new UpsertAssetMetaCategoryDto
            {
                DisplayName = "Construction Materials",
                Categories =
                [
                    new UpsertAssetCategoryDto
                    {
                        DisplayName = "Wood",
                        CategoryType = AssetCategoryType.StandaloneAssets,
                        Assets = [new UpsertAssetDto { DisplayName = "Oak Log" }]
                    }
                ]
            }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(_context.AssetMetaCategories.Single().Code, Is.EqualTo("ConstructionMaterials"));
            Assert.That(_context.AssetCategories.Single().Code, Is.EqualTo("ConstructionMaterials.Wood"));
            Assert.That(_context.Assets.Single().Code, Is.EqualTo("ConstructionMaterials.Wood.OakLog"));
        });
    }

    [Test, Description("Re-running an unchanged catalogue updates the same rows rather than inserting a second set, which is what makes re-seeding safe.")]
    public async Task ShouldNotDuplicateRowsWhenRerunUnchanged()
    {
        await UpsertAsync(BuildCatalogue());
        await UpsertAsync(BuildCatalogue());

        Assert.Multiple(() =>
        {
            Assert.That(_context.AssetMetaCategories.Count(), Is.EqualTo(1));
            Assert.That(_context.AssetCategories.Count(), Is.EqualTo(1));
            Assert.That(_context.AssetGroups.Count(), Is.EqualTo(1));
            Assert.That(_context.Assets.Count(), Is.EqualTo(2));
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
            Assert.That(_context.AssetMetaCategories.Count(), Is.EqualTo(1), "The meta-category was duplicated rather than renamed.");
            Assert.That(_context.AssetMetaCategories.Single().DisplayName, Is.EqualTo("Building Materials"));

            Assert.That(_context.AssetCategories.Count(), Is.EqualTo(1), "The category was duplicated rather than renamed.");
            Assert.That(_context.AssetCategories.Single().DisplayName, Is.EqualTo("Timber"));

            Assert.That(_context.AssetGroups.Count(), Is.EqualTo(1), "The group was duplicated rather than renamed.");
            Assert.That(_context.AssetGroups.Single().DisplayName, Is.EqualTo("Trunks"));
        });
    }

    [Test, Description("A category's subject scope is stored and then updated in place, so marking an existing category as a per-other-player template does not need a new row.")]
    public async Task ShouldStoreAndUpdateTheCategorySubjectScope()
    {
        await UpsertAsync(BuildCatalogue());

        Assert.That(_context.AssetCategories.Single().SubjectScope, Is.EqualTo(AssetSubjectScope.Own),
            "a category that says nothing about a subject counts what the reading Player holds");

        _context.ChangeTracker.Clear();

        await UpsertAsync(BuildCatalogue(subjectScope: AssetSubjectScope.PerOtherPlayer));

        Assert.Multiple(() =>
        {
            Assert.That(_context.AssetCategories.Count(), Is.EqualTo(1), "The category was duplicated rather than updated.");
            Assert.That(_context.AssetCategories.Single().SubjectScope, Is.EqualTo(AssetSubjectScope.PerOtherPlayer));
        });
    }

    [Test, Description("Two rows resolving to the same code are refused before anything is written, so a collision cannot half-apply.")]
    public async Task ShouldRefuseACatalogueWithCollidingCodes()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertAssetsRequest(
            [
                new UpsertAssetMetaCategoryDto
                {
                    Code = "ConstructionMaterials",
                    DisplayName = "Construction Materials",
                    Categories =
                    [
                        new UpsertAssetCategoryDto
                        {
                            Code = "ConstructionMaterials.Wood",
                            DisplayName = "Wood",
                            CategoryType = AssetCategoryType.StandaloneAssets,
                            Assets =
                            [
                                new UpsertAssetDto { Code = "Duplicated", DisplayName = "Oak Log" },
                                new UpsertAssetDto { Code = "Duplicated", DisplayName = "Ash Log" }
                            ]
                        }
                    ]
                }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(_context.Assets.Count(), Is.Zero, "Rows were written despite the collision being refused.");
        });
    }

    [Test, Description("Two codes differing only in case are refused as a collision, because the database they are headed for indexes codes case-insensitively and would reject the second of them anyway.")]
    public async Task ShouldRefuseACatalogueWhoseCodesCollideOnlyInCase()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertAssetsRequest(
            [
                new UpsertAssetMetaCategoryDto
                {
                    Code = "ConstructionMaterials",
                    DisplayName = "Construction Materials",
                    Categories =
                    [
                        new UpsertAssetCategoryDto
                        {
                            Code = "ConstructionMaterials.Wood",
                            DisplayName = "Wood",
                            CategoryType = AssetCategoryType.StandaloneAssets,
                            Assets =
                            [
                                new UpsertAssetDto { Code = "Duplicated", DisplayName = "Oak Log" },
                                new UpsertAssetDto { Code = "DUPLICATED", DisplayName = "Ash Log" }
                            ]
                        }
                    ]
                }
            ]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False, "The collision was accepted, and would have reached the unique index as an insert.");
            Assert.That(result.Errors, Has.Some.Contains("Duplicated"), "The error has to name the colliding code, since that is all the caller has to go on.");
            Assert.That(_context.Assets.Count(), Is.Zero, "Rows were written despite the collision being refused.");
        });
    }

    [Test, Description("Assets are given sort orders spaced by ten from their position in the list, so file order is display order.")]
    public async Task ShouldDeriveAssetSortOrderFromListPosition()
    {
        await UpsertAsync(BuildCatalogue());

        Assert.Multiple(() =>
        {
            Assert.That(_context.Assets.Single(r => r.DisplayName == "Oak Log").SortOrder, Is.EqualTo(10));
            Assert.That(_context.Assets.Single(r => r.DisplayName == "Ash Log").SortOrder, Is.EqualTo(20));
        });
    }

    [Test, Description("A supplied asset sort order is kept, so an explicit ordering is not overwritten by list position.")]
    public async Task ShouldKeepASuppliedAssetSortOrder()
    {
        await UpsertAsync(BuildCatalogue(firstAssetSortOrder: 500));

        Assert.That(_context.Assets.Single(r => r.DisplayName == "Oak Log").SortOrder, Is.EqualTo(500));
    }

    [Test, Description("Each grade of a graded asset is stored as its own row under one display name, told apart by its code and grade.")]
    public async Task ShouldStoreOneRowPerGradeOfAGradedAsset()
    {
        await UpsertAsync(
        [
            new UpsertAssetMetaCategoryDto
            {
                Code = "Weapons",
                DisplayName = "Weapons",
                Categories =
                [
                    new UpsertAssetCategoryDto
                    {
                        Code = "Weapons.MeleeWeapons",
                        DisplayName = "Melee Weapons",
                        CategoryType = AssetCategoryType.StandaloneAssets,
                        Assets =
                        [
                            GradedKnife(AssetGrade.Low),
                            GradedKnife(AssetGrade.Medium),
                            GradedKnife(AssetGrade.High)
                        ]
                    }
                ]
            }
        ]);

        Asset[] knives = [.. _context.Assets.Where(r => r.DisplayName == "Bone Knife")];

        Assert.Multiple(() =>
        {
            Assert.That(knives, Has.Length.EqualTo(3));
            Assert.That(knives.Select(k => k.AssetGrade), Is.EquivalentTo(new[] { AssetGrade.Low, AssetGrade.Medium, AssetGrade.High }));
            Assert.That(knives.Select(k => k.Code), Is.Unique);
            Assert.That(knives.Select(k => k.IsGradedAsset), Is.All.True);
        });
    }

    [Test, Description("An update that omits the description and icon leaves the stored ones alone, so a partial edit cannot blank flavour text.")]
    public async Task ShouldLeaveDescriptionAndIconAloneWhenAnUpdateOmitsThem()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(includeAssetDetail: false));

        Asset oakLog = _context.Assets.Single(r => r.DisplayName == "Oak Log");

        Assert.Multiple(() =>
        {
            Assert.That(oakLog.Description, Is.EqualTo("A dense hardwood."));
            Assert.That(oakLog.IconPath, Is.EqualTo("/resources/wood/oak-log_40px.png"));
        });
    }

    private async Task UpsertAsync(IReadOnlyCollection<UpsertAssetMetaCategoryDto> metaCategories)
    {
        ServiceResultBase result = await _handler.Handle(new UpsertAssetsRequest(metaCategories), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"Upsert failed: {string.Join("; ", result.Errors)}");

        // The handler shares this context, so its tracked entities would otherwise satisfy the next assertion without a round trip.
        _context.ChangeTracker.Clear();
    }

    /// <summary>
    /// One meta-category holding one grouped category with a group of two logs, which is enough shape to exercise all four levels at once.
    /// </summary>
    /// <param name="includeAssetDetail">False omits each asset's description and icon, to exercise the partial-update rule.</param>
    private static IReadOnlyCollection<UpsertAssetMetaCategoryDto> BuildCatalogue(
        string metaDisplayName = "Construction Materials",
        string categoryDisplayName = "Wood",
        string groupDisplayName = "Logs",
        int? firstAssetSortOrder = null,
        bool includeAssetDetail = true,
        AssetSubjectScope subjectScope = AssetSubjectScope.Own)
    {
        return
        [
            new UpsertAssetMetaCategoryDto
            {
                Code = "ConstructionMaterials",
                DisplayName = metaDisplayName,
                IconPath = "/resources/wood/oak-log_40px.png",
                Categories =
                [
                    new UpsertAssetCategoryDto
                    {
                        Code = "ConstructionMaterials.Wood",
                        DisplayName = categoryDisplayName,
                        CategoryType = AssetCategoryType.GroupedAssets,
                        SubjectScope = subjectScope,
                        Groups =
                        [
                            new UpsertAssetGroupDto
                            {
                                Code = "ConstructionMaterials.Wood.Logs",
                                DisplayName = groupDisplayName,
                                Assets =
                                [
                                    new UpsertAssetDto
                                    {
                                        Code = "ConstructionMaterials.Wood.Logs.OakLog",
                                        DisplayName = "Oak Log",
                                        IconPath = includeAssetDetail ? "/resources/wood/oak-log_40px.png" : null,
                                        Description = includeAssetDetail ? "A dense hardwood." : null,
                                        SortOrder = firstAssetSortOrder
                                    },
                                    new UpsertAssetDto
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

    private static UpsertAssetDto GradedKnife(AssetGrade grade)
    {
        return new UpsertAssetDto
        {
            Code = $"Weapons.MeleeWeapons.BoneKnife.{grade}",
            DisplayName = "Bone Knife",
            IsGradedAsset = true,
            AssetGrade = grade
        };
    }
}
