namespace AscendantEmpire.Domain.Enums.Settlements;

/// <summary>
/// How far up the settlement ladder a Settlement sits.
/// </summary>
/// <remarks>
/// <para>
/// The tiers of <c>docs/design/settlements.md</c>, in growth order. Only <see cref="Camp"/> and <see cref="Village"/> can be founded today - the two the settler's
/// capabilities unlock - and the rest are reached by growth, which is not built.
/// </para>
/// <para>
/// The tier is stored on the Settlement rather than inferred from its <c>Structure</c>, because growth changes the tier and the artwork together and the row has to say
/// which it is without a join. The two are kept in step by <c>SettlementTierRules</c>, which is the one place that maps a tier to the structure code standing for it.
/// </para>
/// <para>
/// Values are assigned explicitly and must never be renumbered: they are persisted on every Settlement row.
/// </para>
/// </remarks>
public enum SettlementTier
{
    /// <summary>The smallest settlement, established by a Force carrying the <c>create-early-camp</c> capability.</summary>
    Camp = 1,

    /// <summary>Founded by a Force carrying the <c>create-settlement</c> capability.</summary>
    Village = 2,

    /// <summary>Not foundable; reached by growth, which is not built.</summary>
    Hamlet = 3,

    /// <summary>Not foundable; reached by growth, which is not built.</summary>
    Town = 4,

    /// <summary>Not foundable; reached by growth, which is not built.</summary>
    City = 5,

    /// <summary>Not foundable; reached by growth, which is not built.</summary>
    MajorCity = 6,
}
