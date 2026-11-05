using AscendantEmpire.Application.Features.Assets.Classes;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="SettlementStorageCapacity"/>: that a class fills its dedicated store before the generic pool, and that the pool is shared.
/// </summary>
/// <remarks>
/// No database. The arithmetic is pure, and the figures here are raw rather than whole units because what is pinned is the comparison a credit makes, not how a
/// quantity is displayed. Storage class ids are arbitrary: nothing here reads the catalogue.
/// </remarks>
[TestFixture]
internal class SettlementStorageCapacityTests
{
    private const int _STORAGE_YARD_ID = 7;
    private const int _GRANARY_ID = 9;

    [Test, Description("A class inside its dedicated store has the rest of that store plus the whole pool free, which is what 'dedicated first, then the pool' means.")]
    public void ShouldAddTheWholePoolToWhatIsLeftOfADedicatedStore()
    {
        SettlementStorageCapacity capacity = Build(
            rawPoolCapacity: 1_000,
            dedicated: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 500 },
            held: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 200 });

        Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.EqualTo(1_300));
    }

    [Test, Description("A class past its dedicated store has eaten into the pool, so what is free is the pool's remainder alone.")]
    public void ShouldReportOnlyThePoolRemainderWhenTheDedicatedStoreIsFull()
    {
        SettlementStorageCapacity capacity = Build(
            rawPoolCapacity: 1_000,
            dedicated: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 500 },
            held: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 800 });

        Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.EqualTo(700));
    }

    [Test, Description("Two classes overflowing share one pool, so each sees what the other has already put in it.")]
    public void ShouldShareOnePoolBetweenTwoOverflowingClasses()
    {
        SettlementStorageCapacity capacity = Build(
            rawPoolCapacity: 1_000,
            dedicated: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 500, [_GRANARY_ID] = 100 },
            held: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 800, [_GRANARY_ID] = 400 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.EqualTo(400));
            Assert.That(capacity.SpaceAvailable(_GRANARY_ID), Is.EqualTo(400));
        }
    }

    [Test, Description("A class with no dedicated store has the pool and nothing else, which is every class in the catalogue until buildings exist.")]
    public void ShouldGiveAClassWithNoDedicatedStoreThePoolAlone()
    {
        SettlementStorageCapacity capacity = Build(
            rawPoolCapacity: 1_000,
            dedicated: new Dictionary<int, long>(),
            held: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 300 });

        Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.EqualTo(700));
    }

    [Test, Description("A settlement already over its caps reports no free capacity rather than a negative one, since enforcement is on the credit path only.")]
    public void ShouldReportNoFreeCapacityWhenTheHoldingIsAlreadyOverCap()
    {
        SettlementStorageCapacity capacity = Build(
            rawPoolCapacity: 1_000,
            dedicated: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 500 },
            held: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 2_000 });

        Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.Zero);
    }

    [Test, Description("An overdrawn pool does not swallow an untouched dedicated store, because another class being over cap is not this class's problem.")]
    public void ShouldStillOfferADedicatedStoreWhenThePoolIsOverdrawn()
    {
        // Held above a cap is reachable without a credit: enforcement is on the credit path alone, so a holding that predates its cap persists.
        SettlementStorageCapacity capacity = Build(
            rawPoolCapacity: 1_000,
            dedicated: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 500 },
            held: new Dictionary<int, long> { [_STORAGE_YARD_ID] = 0, [_GRANARY_ID] = 1_500 });

        Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.EqualTo(500));
    }

    [Test, Description("Storing places what fits, reports that figure, and leaves the class full, so a caller cannot credit more than the store holds.")]
    public void ShouldPlaceOnlyWhatFitsWhenStoringMoreThanIsFree()
    {
        SettlementStorageCapacity capacity = Build(rawPoolCapacity: 1_000);

        long placed = capacity.Store(_STORAGE_YARD_ID, 1_500);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(placed, Is.EqualTo(1_000));
            Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.Zero);
        }
    }

    [Test, Description("Successive stores consume the same pool, which is what stops two Assets of one class each being told the whole pool is free.")]
    public void ShouldConsumeThePoolAcrossSuccessiveStores()
    {
        SettlementStorageCapacity capacity = Build(rawPoolCapacity: 1_000);

        long firstPlaced = capacity.Store(_STORAGE_YARD_ID, 400);
        long secondPlaced = capacity.Store(_GRANARY_ID, 400);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPlaced, Is.EqualTo(400));
            Assert.That(secondPlaced, Is.EqualTo(400));
            Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.EqualTo(200));
        }
    }

    [TestCase(0, Description = "Storing nothing places nothing and leaves the capacity untouched.")]
    [TestCase(-50, Description = "A negative amount places nothing rather than crediting capacity backwards.")]
    public void ShouldPlaceNothingWhenStoringAZeroOrNegativeAmount(long rawAmount)
    {
        SettlementStorageCapacity capacity = Build(rawPoolCapacity: 1_000);

        long placed = capacity.Store(_STORAGE_YARD_ID, rawAmount);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(placed, Is.Zero);
            Assert.That(capacity.SpaceAvailable(_STORAGE_YARD_ID), Is.EqualTo(1_000));
        }
    }

    private static SettlementStorageCapacity Build(
        long rawPoolCapacity,
        IReadOnlyDictionary<int, long>? dedicated = null,
        IReadOnlyDictionary<int, long>? held = null)
    {
        return new SettlementStorageCapacity(
            rawPoolCapacity,
            dedicated ?? new Dictionary<int, long>(),
            held ?? new Dictionary<int, long>());
    }
}
