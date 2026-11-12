using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests what the supply-set seed dispatches and how it matches a set that already exists.
/// </summary>
/// <remarks>
/// In-memory: what needs the database is the code-to-id match and the catalogue precondition. The sets themselves are authored data, covered by
/// <see cref="SupplySetSeedDataTests"/>. The upsert is dispatched through the mediator, which is substituted here so these tests pin what the seed sent
/// rather than re-testing <c>SupplySetHandlerTests</c>' writes.
/// </remarks>
[TestFixture]
internal class SeedSupplySetsHandlerTests : InMemoryTestBase
{
    private const string _BERRIES = "Foodstuffs.PlantFoods.Berries";

    private AscendantEmpireContext _context = null!;
    private IMediator _mediator = null!;

    [SetUp]
    public async Task SetUpHandler()
    {
        _context = await DbFactory.CreateDbContextAsync();

        _mediator = Substitute.For<IMediator>();
        _mediator.Send(Arg.Any<CreateSupplySetRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<SupplySetView>.Succeed(new SupplySetView
            {
                Id = 1,
                Code = "seeded",
                DisplayName = "seeded",
                SupplyType = SupplyType.Food
            }));

        _mediator.Send(Arg.Any<UpdateSupplySetRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<SupplySetView>.Succeed(new SupplySetView
            {
                Id = 1,
                Code = "seeded",
                DisplayName = "seeded",
                SupplyType = SupplyType.Food
            }));

        await AddAssetAsync(_BERRIES);
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    #region Dispatch

    [Test, Description("The seed sends every authored set, since none of them is worked out from the catalogue.")]
    public async Task Seed_DispatchesEveryAuthoredSet()
    {
        ServiceResultBase result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(DispatchedSets().Select(set => set.Code).Order(), Is.EqualTo(SupplySetSeedData.BuildSets().Select(set => set.Code).Order()));
        }
    }

    [Test, Description("A set is dispatched with the members the seed data authored, rather than any worked out from the Assets in the database.")]
    public async Task Seed_DispatchesTheAuthoredMembersUntouched()
    {
        await HandleAsync();

        UpsertSupplySetDto authored = SupplySetSeedData.BuildSets().Single(set => set.Code == SupplySetSeedData.HUMAN_FOOD);
        UpsertSupplySetDto dispatched = DispatchedSets().Single(set => set.Code == SupplySetSeedData.HUMAN_FOOD);

        // Berries alone sit in the catalogue, so a membership read off the Assets table would be one member long.
        Assert.That(
            dispatched.Assets.Select(asset => asset.AssetCode).Order(),
            Is.EqualTo(authored.Assets.Select(asset => asset.AssetCode).Order()));
    }

    #endregion

    #region Idempotence

    [Test, Description("A set that already exists is updated by its id rather than created again, so re-seeding does not fail on its unique code.")]
    public async Task Seed_UpdatesAnExistingSetRatherThanCreatingIt()
    {
        _context.SupplySets.Add(new SupplySet
        {
            Code = SupplySetSeedData.HUMAN_FOOD,
            DisplayName = "Human Food",
            SupplyType = SupplyType.Food
        });

        await _context.SaveChangesAsync();

        int existingId = _context.SupplySets.Single().Id;

        await HandleAsync();

        List<UpdateSupplySetRequest> updates = UpdateRequests();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updates.Select(update => update.Set.Code), Does.Contain(SupplySetSeedData.HUMAN_FOOD));
            Assert.That(updates.Single(update => update.Set.Code == SupplySetSeedData.HUMAN_FOOD).Id, Is.EqualTo(existingId));
            Assert.That(CreateRequests().Select(create => create.Set.Code), Does.Not.Contain(SupplySetSeedData.HUMAN_FOOD));
        }
    }

    [Test, Description("An unseeded Asset catalogue fails the step by name rather than letting every member fail to resolve opaquely.")]
    public async Task Seed_FailsWhenTheAssetCatalogueIsEmpty()
    {
        _context.Assets.RemoveRange(_context.Assets);
        await _context.SaveChangesAsync();

        ServiceResultBase result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Assets"));
        }
    }

    #endregion

    #region Helpers

    private async Task<ServiceResultBase> HandleAsync()
    {
        var handler = new SeedSupplySetsHandler(_context, _mediator);

        return await handler.Handle(new SeedSupplySetsRequest(), CancellationToken.None);
    }

    private List<CreateSupplySetRequest> CreateRequests()
    {
        return [.. _mediator.ReceivedCalls()
            .Select(call => call.GetArguments()[0])
            .OfType<CreateSupplySetRequest>()];
    }

    private List<UpdateSupplySetRequest> UpdateRequests()
    {
        return [.. _mediator.ReceivedCalls()
            .Select(call => call.GetArguments()[0])
            .OfType<UpdateSupplySetRequest>()];
    }

    /// <summary>
    /// Every set payload the seed sent, whether it was created or updated.
    /// </summary>
    private List<UpsertSupplySetDto> DispatchedSets()
    {
        return
        [
            .. CreateRequests().Select(create => create.Set),
            .. UpdateRequests().Select(update => update.Set)
        ];
    }

    /// <summary>
    /// Adds an Asset so the catalogue reads as seeded. The owning category, meta-category and storage class are referenced by id only - in-memory enforces
    /// no foreign key, and the seed reads none of them.
    /// </summary>
    private async Task AddAssetAsync(string code)
    {
        _context.Assets.Add(new Asset
        {
            Code = code,
            DisplayName = code.Split('.').Last(),
            IconPath = "/resources/none.svg",
            IsGradedAsset = false,
            SortOrder = _context.Assets.Count(),
            AssetMetaCategoryId = 1,
            AssetCategoryId = 1,
            StorageClassId = 1
        });

        await _context.SaveChangesAsync();
    }

    #endregion
}
