namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// What one Settlement can still take of each storage class: its dedicated stores, the generic pool they overflow into, and what it already holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two levels, and placement is derived rather than recorded.</b> A class fills its own dedicated store first and the generic pool second, so what sits in the
/// pool is simply what each class holds above its own cap. That is order-independent, which is why no balance row carries a "where is this kept" column and why
/// there is nothing to reconcile.
/// </para>
/// <para>
/// <b>Capacity does not bind per network.</b> A network is what a Settlement may draw <i>on</i>; it is not somewhere to put things. A credit spread across a network
/// takes one of these per Settlement in it - see <c>IAssetBankCreditService</c>.
/// </para>
/// <para>
/// <b>One instance per Settlement per operation, and not thread-safe.</b> <see cref="Store"/> mutates what is held so that the next question sees the last answer -
/// that is the whole point of it, and it is what stops two Assets of one class each being told the whole pool is free.
/// </para>
/// <para>Every figure is raw scaled on the <c>ScaledAmount.Scale</c> convention of 1,000.</para>
/// </remarks>
internal sealed class SettlementStorageCapacity
{
    private readonly long _rawPoolCapacity;

    private readonly IReadOnlyDictionary<int, long> _rawDedicatedCapacityByClassId;

    private readonly Dictionary<int, long> _rawHeldByClassId;

    /// <param name="rawPoolCapacity">The tier-granted generic pool - the storehouse - which every class overflows into.</param>
    /// <param name="rawDedicatedCapacityByClassId">What each class has a store of its own for. Empty for every Settlement today: a dedicated store is granted by a building, and no building model exists.</param>
    /// <param name="rawHeldByClassId">What the Settlement already holds of each class. A class absent from either dictionary stands at 0.</param>
    public SettlementStorageCapacity(
        long rawPoolCapacity,
        IReadOnlyDictionary<int, long> rawDedicatedCapacityByClassId,
        IReadOnlyDictionary<int, long> rawHeldByClassId)
    {
        ArgumentNullException.ThrowIfNull(rawDedicatedCapacityByClassId);
        ArgumentNullException.ThrowIfNull(rawHeldByClassId);

        _rawPoolCapacity = rawPoolCapacity;
        _rawDedicatedCapacityByClassId = rawDedicatedCapacityByClassId;
        _rawHeldByClassId = new Dictionary<int, long>(rawHeldByClassId);
    }

    /// <summary>
    /// How much more of this storage class the Settlement can take.
    /// </summary>
    /// <remarks>
    /// <b>Each level is floored at zero before they are added</b>, not the total.
    /// </remarks>
    public long SpaceAvailable(int storageClassId)
    {
        long dedicatedCapacity = CapacityFor(storageClassId);
        long held = HeldOf(storageClassId);

        long dedicatedFree = Math.Max(0, dedicatedCapacity - held);
        long poolFree = Math.Max(0, _rawPoolCapacity - SpaceUsed());

        return dedicatedFree + poolFree;
    }

    /// <summary>
    /// Places as much of <paramref name="rawAmount"/> as will fit, and reports what was placed.
    /// </summary>
    /// <returns>What was placed, which is 0 when the class is full.</returns>
    public long Store(int storageClassId, long rawAmount)
    {
        if (rawAmount <= 0)
        {
            return 0;
        }

        long placed = Math.Min(rawAmount, SpaceAvailable(storageClassId));

        if (placed > 0)
        {
            _rawHeldByClassId[storageClassId] = HeldOf(storageClassId) + placed;
        }

        return placed;
    }

    /// <summary>
    /// How much of the generic pool is in use: what every class holds above its own dedicated store.
    /// </summary>
    private long SpaceUsed()
    {
        return _rawHeldByClassId.Sum(held => Math.Max(0, held.Value - CapacityFor(held.Key)));
    }

    private long CapacityFor(int storageClassId)
    {
        return _rawDedicatedCapacityByClassId.TryGetValue(storageClassId, out long capacity) ? capacity : 0;
    }

    private long HeldOf(int storageClassId)
    {
        return _rawHeldByClassId.TryGetValue(storageClassId, out long held) ? held : 0;
    }
}
