using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests how a supply set upsert resolves its members and what it writes, and what the catalogue read returns.
/// </summary>
/// <remarks>
/// In-memory: these paths stick to LINQ, <c>Add</c>, <c>RemoveRange</c> and <c>SaveChanges</c>, and touch no complex type. Code uniqueness is checked by
/// the handler rather than left to the unique index, which in-memory could not reproduce.
/// </remarks>
[TestFixture]
internal class SupplySetHandlerTests : InMemoryTestBase
{
    private const string _BERRIES = "Foodstuffs.PlantFoods.Berries";
    private const string _FODDER = "RawMaterials.RawMaterials.Fodder";

    private AscendantEmpireContext _context = null!;
    private CreateSupplySetHandler _createHandler = null!;
    private UpdateSupplySetHandler _updateHandler = null!;
    private GetSupplySetsHandler _getHandler = null!;

    [SetUp]
    public async Task SetUpHandlers()
    {
        _context = await DbFactory.CreateDbContextAsync();

        _createHandler = new CreateSupplySetHandler(new NullLogger<CreateSupplySetHandler>(), _context);
        _updateHandler = new UpdateSupplySetHandler(new NullLogger<UpdateSupplySetHandler>(), _context);
        _getHandler = new GetSupplySetsHandler(_context);

        await SeedAssetsAsync(_BERRIES, _FODDER);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    #region Create

    [Test, Description("Creating a set resolves its members by Asset code and returns the persisted view.")]
    public async Task CreateSupplySet_ResolvesAssetCodesAndReturnsView()
    {
        ServiceResult<SupplySetView> result = await _createHandler.Handle(
            new CreateSupplySetRequest(BuildSet("human-food", SupplyType.Food, [Member(_BERRIES, amountPerUnit: 1_670)])),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.Code, Is.EqualTo("human-food"));
            Assert.That(result.SuccessValue!.Assets.Single().AssetCode, Is.EqualTo(_BERRIES));
            Assert.That(result.SuccessValue!.Assets.Single().AmountPerUnit, Is.EqualTo(1_670));
        }
    }

    [Test, Description("A set whose code already exists is a conflict rather than a silent update.")]
    public async Task CreateSupplySet_RejectsDuplicateCodeAsConflict()
    {
        await CreateAsync(BuildSet("human-food", SupplyType.Food, [Member(_BERRIES)]));

        ServiceResult<SupplySetView> result = await _createHandler.Handle(
            new CreateSupplySetRequest(BuildSet("human-food", SupplyType.Food, [Member(_FODDER)])),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
        }
    }

    [Test, Description("An unknown Asset code fails the request and names every code that did not resolve.")]
    public async Task CreateSupplySet_NamesEveryUnresolvedAssetCode()
    {
        ServiceResult<SupplySetView> result = await _createHandler.Handle(
            new CreateSupplySetRequest(BuildSet("human-food", SupplyType.Food, [Member("Nope.One"), Member("Nope.Two")])),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Nope.One"));
            Assert.That(result.Errors, Has.Some.Contains("Nope.Two"));
        }
    }

    #endregion

    #region Update

    [Test, Description("Updating a set reconciles its members to exactly the payload, dropping those left out.")]
    public async Task UpdateSupplySet_ReconcilesMembersToPayload()
    {
        SupplySetView created = await CreateAsync(BuildSet("human-food", SupplyType.Food, [Member(_BERRIES), Member(_FODDER)]));

        ServiceResult<SupplySetView> result = await _updateHandler.Handle(
            new UpdateSupplySetRequest(created.Id, BuildSet("human-food", SupplyType.Food, [Member(_FODDER, amountPerUnit: 2_000)])),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.Assets.Single().AssetCode, Is.EqualTo(_FODDER));
            Assert.That(result.SuccessValue!.Assets.Single().AmountPerUnit, Is.EqualTo(2_000));
        }
    }

    [Test, Description("Updating a set that does not exist is a not-found rather than a silent create.")]
    public async Task UpdateSupplySet_RejectsAnUnknownId()
    {
        ServiceResult<SupplySetView> result = await _updateHandler.Handle(
            new UpdateSupplySetRequest(404, BuildSet("human-food", SupplyType.Food, [Member(_BERRIES)])),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [Test, Description("A set components already require cannot change its supply type, which would silently turn every one of those requirements into a different need.")]
    public async Task Update_RefusesATypeChangeWhileComponentsRequireTheSet()
    {
        SupplySetView created = await CreateAsync(BuildSet("human-food", SupplyType.Food, [Member(_BERRIES)]));

        await RequireSetAsync(created.Id);

        ServiceResult<SupplySetView> result = await _updateHandler.Handle(
            new UpdateSupplySetRequest(created.Id, BuildSet("human-food", SupplyType.Fuel, [Member(_BERRIES)])),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("component"));
        }
    }

    [Test, Description("Only the type is pinned while a set is required: its name, code and members stay editable.")]
    public async Task Update_AllowsEverythingButTheTypeWhileComponentsRequireTheSet()
    {
        SupplySetView created = await CreateAsync(BuildSet("human-food", SupplyType.Food, [Member(_BERRIES)]));

        await RequireSetAsync(created.Id);

        ServiceResult<SupplySetView> result = await _updateHandler.Handle(
            new UpdateSupplySetRequest(created.Id, BuildSet("human-food", SupplyType.Food, [Member(_BERRIES), Member(_FODDER)])),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.Assets, Has.Count.EqualTo(2));
        }
    }

    #endregion

    #region Read

    [Test, Description("Reading the catalogue returns sets ordered by code, each with its members.")]
    public async Task GetSupplySets_ReturnsSetsOrderedByCode()
    {
        await CreateAsync(BuildSet("mount-fodder", SupplyType.Fuel, [Member(_FODDER)]));
        await CreateAsync(BuildSet("human-food", SupplyType.Food, [Member(_BERRIES)]));

        ServiceResult<GetSupplySetsResult> result = await _getHandler.Handle(new GetSupplySetsRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.SupplySets.Select(set => set.Code), Is.EqualTo(["human-food", "mount-fodder"]));
            Assert.That(result.SuccessValue!.SupplySets.First().Assets.Single().AssetCode, Is.EqualTo(_BERRIES));
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Points a component requirement at the set, so the set reads as one in use.
    /// </summary>
    /// <remarks>
    /// The component id is a bare number rather than a seeded component: the guard counts requirement rows and never loads the component behind one, and a
    /// real <c>UnitComponent</c> would drag in the <c>DamageProfile</c> complex type that the in-memory provider throws on.
    /// </remarks>
    private async Task RequireSetAsync(int supplySetId)
    {
        _context.UnitComponentSupplyRequirements.Add(new UnitComponentSupplyRequirement
        {
            UnitComponentId = 1,
            SupplySetId = supplySetId,
            Quantity = 1_000
        });

        await _context.SaveChangesAsync();
    }

    private async Task<SupplySetView> CreateAsync(UpsertSupplySetDto set)
    {
        ServiceResult<SupplySetView> result = await _createHandler.Handle(new CreateSupplySetRequest(set), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"Creating '{set.Code}' failed: {string.Join("; ", result.Errors)}");

        return result.SuccessValue!;
    }

    /// <summary>
    /// Adds the Assets a set's members resolve against. The owning category, meta-category and storage class are referenced by id only - in-memory
    /// enforces no foreign key, and nothing here reads them.
    /// </summary>
    private async Task SeedAssetsAsync(params string[] codes)
    {
        int sortOrder = 0;

        foreach (string code in codes)
        {
            _context.Assets.Add(new Domain.Entities.Assets.Asset
            {
                Code = code,
                DisplayName = code.Split('.').Last(),
                IconPath = "/resources/none.svg",
                IsGradedAsset = false,
                SortOrder = sortOrder++,
                AssetMetaCategoryId = 1,
                AssetCategoryId = 1,
                StorageClassId = 1
            });
        }

        await _context.SaveChangesAsync();
    }

    private static UpsertSupplySetDto BuildSet(string code, SupplyType supplyType, IReadOnlyCollection<UpsertSupplySetAssetDto> assets)
    {
        return new UpsertSupplySetDto
        {
            Code = code,
            DisplayName = code,
            SupplyType = supplyType,
            Assets = assets
        };
    }

    private static UpsertSupplySetAssetDto Member(string assetCode, long amountPerUnit = 1_000, int selectionWeight = 1)
    {
        return new UpsertSupplySetAssetDto
        {
            AssetCode = assetCode,
            AmountPerUnit = amountPerUnit,
            SelectionWeight = selectionWeight
        };
    }

    #endregion
}
