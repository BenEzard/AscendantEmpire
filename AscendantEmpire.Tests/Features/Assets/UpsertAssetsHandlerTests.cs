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
    /// <summary>
    /// The two storage classes every catalogue built here names. Two, because moving an Asset from one class to another needs somewhere to move it to.
    /// </summary>
    private const string _STORAGE_YARD_CODE = "StorageYard";
    private const string _ARMOURY_CODE = "Armoury";

    private AscendantEmpireContext _context = null!;
    private UpsertAssetsHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _handler = new UpsertAssetsHandler(new NullLogger<UpsertAssetsHandler>(), _context);

        // Asset.StorageClassId is a non-null FK resolved against committed rows, so the classes have to exist before any asset payload names one. Seeded
        // through the real upsert rather than added directly, so these rows match what the seed writes.
        var storageClasses = new UpsertStorageClassesHandler(new NullLogger<UpsertStorageClassesHandler>(), _context);

        ServiceResultBase seeded = await storageClasses.Handle(
            new UpsertStorageClassesRequest(
            [
                new UpsertStorageClassDto { Code = _STORAGE_YARD_CODE, DisplayName = "Storage Yard" },
                new UpsertStorageClassDto { Code = _ARMOURY_CODE, DisplayName = "Armoury" }
            ]),
            CancellationToken.None);

        Assert.That(seeded.HasSucceeded, Is.True, () => string.Join("; ", seeded.Errors));

        _context.ChangeTracker.Clear();
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.AssetMetaCategories.Single().Code, Is.EqualTo("ConstructionMaterials"));
            Assert.That(_context.AssetCategories.Single(c => c.DisplayName == "Wood").Code, Is.EqualTo("ConstructionMaterials.Wood"));
            Assert.That(_context.AssetGroups.Single().Code, Is.EqualTo("ConstructionMaterials.Wood.Logs"));
            Assert.That(_context.Assets.Single(r => r.DisplayName == "Oak Log").Code, Is.EqualTo("ConstructionMaterials.Wood.Logs.OakLog"));
        }
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
                        Assets = [new UpsertAssetDto { DisplayName = "Oak Log", StorageClassCode = _STORAGE_YARD_CODE }]
                    }
                ]
            }
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.AssetMetaCategories.Single().Code, Is.EqualTo("ConstructionMaterials"));
            Assert.That(_context.AssetCategories.Single().Code, Is.EqualTo("ConstructionMaterials.Wood"));
            Assert.That(_context.Assets.Single().Code, Is.EqualTo("ConstructionMaterials.Wood.OakLog"));
        }
    }

    [Test, Description("Re-running an unchanged catalogue updates the same rows rather than inserting a second set, which is what makes re-seeding safe.")]
    public async Task ShouldNotDuplicateRowsWhenRerunUnchanged()
    {
        await UpsertAsync(BuildCatalogue());
        await UpsertAsync(BuildCatalogue());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.AssetMetaCategories.Count(), Is.EqualTo(1));
            Assert.That(_context.AssetCategories.Count(), Is.EqualTo(1));
            Assert.That(_context.AssetGroups.Count(), Is.EqualTo(1));
            Assert.That(_context.Assets.Count(), Is.EqualTo(2));
        }
    }

    [Test, Description("Renaming a display name while keeping the code renames the existing rows at every level instead of orphaning them behind new ones.")]
    public async Task ShouldRenameRatherThanDuplicateWhenOnlyTheDisplayNameChanges()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(
            metaDisplayName: "Building Materials",
            categoryDisplayName: "Timber",
            groupDisplayName: "Trunks"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.AssetMetaCategories.Count(), Is.EqualTo(1), "The meta-category was duplicated rather than renamed.");
            Assert.That(_context.AssetMetaCategories.Single().DisplayName, Is.EqualTo("Building Materials"));

            Assert.That(_context.AssetCategories.Count(), Is.EqualTo(1), "The category was duplicated rather than renamed.");
            Assert.That(_context.AssetCategories.Single().DisplayName, Is.EqualTo("Timber"));

            Assert.That(_context.AssetGroups.Count(), Is.EqualTo(1), "The group was duplicated rather than renamed.");
            Assert.That(_context.AssetGroups.Single().DisplayName, Is.EqualTo("Trunks"));
        }
    }

    [Test, Description("A category's subject scope is stored and then updated in place, so marking an existing category as a per-other-player template does not need a new row.")]
    public async Task ShouldStoreAndUpdateTheCategorySubjectScope()
    {
        await UpsertAsync(BuildCatalogue());

        Assert.That(_context.AssetCategories.Single().SubjectScope, Is.EqualTo(AssetSubjectScope.Own),
            "a category that says nothing about a subject counts what the reading Player holds");

        _context.ChangeTracker.Clear();

        await UpsertAsync(BuildCatalogue(subjectScope: AssetSubjectScope.PerOtherPlayer));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.AssetCategories.Count(), Is.EqualTo(1), "The category was duplicated rather than updated.");
            Assert.That(_context.AssetCategories.Single().SubjectScope, Is.EqualTo(AssetSubjectScope.PerOtherPlayer));
        }
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
                                new UpsertAssetDto { Code = "Duplicated", DisplayName = "Oak Log", StorageClassCode = _STORAGE_YARD_CODE },
                                new UpsertAssetDto { Code = "Duplicated", DisplayName = "Ash Log", StorageClassCode = _STORAGE_YARD_CODE }
                            ]
                        }
                    ]
                }
            ]),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(_context.Assets.Count(), Is.Zero, "Rows were written despite the collision being refused.");
        }
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
                                new UpsertAssetDto { Code = "Duplicated", DisplayName = "Oak Log", StorageClassCode = _STORAGE_YARD_CODE },
                                new UpsertAssetDto { Code = "DUPLICATED", DisplayName = "Ash Log", StorageClassCode = _STORAGE_YARD_CODE }
                            ]
                        }
                    ]
                }
            ]),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "The collision was accepted, and would have reached the unique index as an insert.");
            Assert.That(result.Errors, Has.Some.Contains("Duplicated"), "The error has to name the colliding code, since that is all the caller has to go on.");
            Assert.That(_context.Assets.Count(), Is.Zero, "Rows were written despite the collision being refused.");
        }
    }

    [Test, Description("Assets are given sort orders spaced by ten from their position in the list, so file order is display order.")]
    public async Task ShouldDeriveAssetSortOrderFromListPosition()
    {
        await UpsertAsync(BuildCatalogue());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.Assets.Single(r => r.DisplayName == "Oak Log").SortOrder, Is.EqualTo(10));
            Assert.That(_context.Assets.Single(r => r.DisplayName == "Ash Log").SortOrder, Is.EqualTo(20));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knives, Has.Length.EqualTo(3));
            Assert.That(knives.Select(k => k.AssetGrade), Is.EquivalentTo(new[] { AssetGrade.Low, AssetGrade.Medium, AssetGrade.High }));
            Assert.That(knives.Select(k => k.Code), Is.Unique);
            Assert.That(knives.Select(k => k.IsGradedAsset), Is.All.True);
        }
    }

    [Test, Description("An update that omits the description and icon leaves the stored ones alone, so a partial edit cannot blank flavour text.")]
    public async Task ShouldLeaveDescriptionAndIconAloneWhenAnUpdateOmitsThem()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(includeAssetDetail: false));

        Asset oakLog = _context.Assets.Single(r => r.DisplayName == "Oak Log");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(oakLog.Description, Is.EqualTo("A dense hardwood."));
            Assert.That(oakLog.IconPath, Is.EqualTo("/resources/wood/oak-log_40px.png"));
        }
    }

    [Test, Description("The supplied storage scope is written on insert and moved on a re-run, so the catalogue is what decides where an asset's figure lives.")]
    public async Task ShouldStoreAndUpdateTheSuppliedStorageScope()
    {
        await UpsertAsync(BuildScopedCatalogue(AssetStorageScope.Player));

        Assert.That(_context.Assets.Single().StorageScope, Is.EqualTo(AssetStorageScope.Player));

        await UpsertAsync(BuildScopedCatalogue(AssetStorageScope.Derived));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.Assets.Single().StorageScope, Is.EqualTo(AssetStorageScope.Derived), "a re-run should move the scope rather than keep the old one");
            Assert.That(_context.Assets.Count(), Is.EqualTo(1), "the re-run matched on code, so it should have updated the row rather than inserted a second");
        }
    }

    [Test, Description("An asset that states no storage scope is held in a settlement bank, which is where all but a handful of the catalogue belongs.")]
    public async Task ShouldDefaultTheStorageScopeToSettlementBank()
    {
        await UpsertAsync(BuildCatalogue());

        Assert.That(
            _context.Assets.Select(asset => asset.StorageScope),
            Is.All.EqualTo(AssetStorageScope.SettlementBank));
    }

    [Test, Description("The naturally occurring flag is written on insert, so the catalogue is what decides which Assets a tile can yield.")]
    public async Task ShouldStoreAndUpdateTheNaturallyOccurringFlag()
    {
        await UpsertAsync(BuildNaturallyOccurringCatalogue(isNaturallyOccurring: true));

        Assert.That(_context.Assets.Single().IsNaturallyOccurring, Is.True);

        await UpsertAsync(BuildNaturallyOccurringCatalogue(isNaturallyOccurring: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.Assets.Single().IsNaturallyOccurring, Is.False, "a re-run should clear the flag rather than keep the old one");
            Assert.That(_context.Assets.Count(), Is.EqualTo(1), "the re-run matched on code, so it should have updated the row rather than inserted a second");
        }
    }

    [Test, Description("An asset that states nothing about natural occurrence is not naturally occurring, so a new catalogue row cannot drift into the yield pickers.")]
    public async Task ShouldDefaultTheNaturallyOccurringFlagToFalse()
    {
        await UpsertAsync(BuildCatalogue());

        Assert.That(_context.Assets.Select(asset => asset.IsNaturallyOccurring), Is.All.False);
    }

    [Test, Description("An Asset is written against the storage class its code names, since capacity binds on that class and a wrong one would misfile the stock.")]
    public async Task ShouldResolveTheStorageClassFromItsCode()
    {
        await UpsertAsync(BuildCatalogue());

        Asset oakLog = _context.Assets
            .Include(asset => asset.StorageClass)
            .Single(asset => asset.DisplayName == "Oak Log");

        Assert.That(oakLog.StorageClass!.Code, Is.EqualTo(_STORAGE_YARD_CODE));
    }

    [Test, Description("A storage class code that resolves to nothing fails the result naming it, rather than throwing out of a dictionary lookup as a 500.")]
    public async Task ShouldRefuseWhenAStorageClassCodeIsUnknown()
    {
        ServiceResultBase result = await _handler.Handle(
            new UpsertAssetsRequest(BuildCatalogue(storageClassCode: "NoSuchClass")),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("NoSuchClass"), "The error has to name the code, since that is all the caller has to go on.");
            Assert.That(_context.Assets.Count(), Is.Zero, "Rows were written despite the class being unresolvable.");
        }
    }

    [Test, Description("A storage class code differing only in case resolves, because the unique index is case-insensitive and the catalogue's casing is what counts.")]
    public async Task ShouldResolveAStorageClassCodeCaseInsensitively()
    {
        await UpsertAsync(BuildCatalogue(storageClassCode: _STORAGE_YARD_CODE.ToUpperInvariant()));

        Asset oakLog = _context.Assets
            .Include(asset => asset.StorageClass)
            .Single(asset => asset.DisplayName == "Oak Log");

        Assert.That(oakLog.StorageClass!.Code, Is.EqualTo(_STORAGE_YARD_CODE));
    }

    [Test, Description("An Asset keeps the class the request names on a re-upsert, so re-seeding re-files nothing by accident.")]
    public async Task ShouldUpdateTheStorageClassOnAnExistingAsset()
    {
        await UpsertAsync(BuildCatalogue());

        await UpsertAsync(BuildCatalogue(storageClassCode: _ARMOURY_CODE));

        Asset oakLog = _context.Assets
            .Include(asset => asset.StorageClass)
            .Single(asset => asset.DisplayName == "Oak Log");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.Assets.Count(), Is.EqualTo(2), "the re-run matched on code, so it should have updated the rows rather than inserting more");
            Assert.That(oakLog.StorageClass!.Code, Is.EqualTo(_ARMOURY_CODE));
        }
    }

    /// <summary>
    /// One standalone category holding a single Asset carrying the given flag, which is the smallest shape that exercises the column.
    /// </summary>
    private static IReadOnlyCollection<UpsertAssetMetaCategoryDto> BuildNaturallyOccurringCatalogue(bool isNaturallyOccurring)
    {
        return
        [
            new UpsertAssetMetaCategoryDto
            {
                Code = "RawMaterials",
                DisplayName = "Raw Materials",
                IconPath = "/resources/other/fishbone.svg",
                Categories =
                [
                    new UpsertAssetCategoryDto
                    {
                        Code = "RawMaterials.RawMaterials",
                        DisplayName = "Raw Materials",
                        CategoryType = AssetCategoryType.StandaloneAssets,
                        Assets =
                        [
                            new UpsertAssetDto
                            {
                                Code = "RawMaterials.RawMaterials.Bone",
                                DisplayName = "Bone",
                                IconPath = "/resources/other/fishbone.svg",
                                IsNaturallyOccurring = isNaturallyOccurring,
                                StorageClassCode = _STORAGE_YARD_CODE
                            }
                        ]
                    }
                ]
            }
        ];
    }

    /// <summary>
    /// One standalone category holding a single Asset at the given storage scope, which is the smallest shape that exercises the column.
    /// </summary>
    private static IReadOnlyCollection<UpsertAssetMetaCategoryDto> BuildScopedCatalogue(AssetStorageScope storageScope)
    {
        return
        [
            new UpsertAssetMetaCategoryDto
            {
                Code = "Society",
                DisplayName = "Society",
                IconPath = "/resources/other/civilian.svg",
                Categories =
                [
                    new UpsertAssetCategoryDto
                    {
                        Code = "Society.Experience",
                        DisplayName = "Experience",
                        CategoryType = AssetCategoryType.StandaloneAssets,
                        Assets =
                        [
                            new UpsertAssetDto
                            {
                                Code = "Society.Experience.Technology",
                                DisplayName = "Technology",
                                IconPath = "/resources/other/bubbling-flask.svg",
                                StorageScope = storageScope,
                                StorageClassCode = _STORAGE_YARD_CODE
                            }
                        ]
                    }
                ]
            }
        ];
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
    /// <param name="storageClassCode">The class both assets name. Defaults to the storage yard; pass another to exercise resolution or a refusal.</param>
    private static IReadOnlyCollection<UpsertAssetMetaCategoryDto> BuildCatalogue(
        string metaDisplayName = "Construction Materials",
        string categoryDisplayName = "Wood",
        string groupDisplayName = "Logs",
        int? firstAssetSortOrder = null,
        bool includeAssetDetail = true,
        AssetSubjectScope subjectScope = AssetSubjectScope.Own,
        string storageClassCode = _STORAGE_YARD_CODE)
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
                                        SortOrder = firstAssetSortOrder,
                                        StorageClassCode = storageClassCode
                                    },
                                    new UpsertAssetDto
                                    {
                                        Code = "ConstructionMaterials.Wood.Logs.AshLog",
                                        DisplayName = "Ash Log",
                                        StorageClassCode = storageClassCode
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
            AssetGrade = grade,
            StorageClassCode = _ARMOURY_CODE
        };
    }
}
