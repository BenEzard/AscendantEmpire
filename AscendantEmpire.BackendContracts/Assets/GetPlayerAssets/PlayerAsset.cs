using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One Asset available to a Player, as the ticker panel needs it.
/// </summary>
/// <remarks>
/// <para>
/// An Asset is here because the Player holds an unlock row for it whose obsoleting Round, if any, is still ahead of the current one. Placement
/// under a group or directly under a category follows the catalogue's exclusive-or: an Asset reaches its meta-category by one path or the other,
/// never both.
/// </para>
/// <para>
/// Most of this record is catalogue data. <see cref="Amount"/> is measured, and the six fields under the placeholder banner are neither - nothing sources
/// them yet.
/// </para>
/// </remarks>
public record PlayerAsset
{
    public required int Id { get; init; }

    /// <summary>
    /// The Asset's unique code, hierarchical and dotted, e.g. "Weapons.MeleeWeapons.BoneKnife.Low".
    /// </summary>
    /// <remarks>
    /// A graded Asset spells its grade out as the last segment, because every grade of it shares one <see cref="DisplayName"/>.
    /// </remarks>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public required string? Description { get; init; }

    /// <summary>
    /// Root-relative URL of the Asset's icon, rendered straight into an image source.
    /// </summary>
    public required string IconPath { get; init; }

    /// <summary>
    /// Whether this Asset comes in grades, in which case the catalogue holds one row per grade under a shared display name.
    /// </summary>
    public required bool IsGradedAsset { get; init; }

    /// <summary>
    /// This row's grade, or None when <see cref="IsGradedAsset"/> is false.
    /// </summary>
    public required AssetGrade AssetGrade { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>
    /// Whether this Asset is never treated as a luxury, however scarce it becomes.
    /// </summary>
    /// <remarks>
    /// A catalogue property and a balance input to the luxury classification, not its result - see <see cref="IsLuxury"/> for that.
    /// </remarks>
    public required bool NeverLuxury { get; init; }

    /// <summary>
    /// How many Rounds until this Asset stops being available, or null while none is scheduled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computed as <c>PlayerUnlockedAsset.ObsoleteOnRound - TurnQueue.CurrentRoundNumber</c>. Rounds, not Turns: the game clock is the Round
    /// number and there is no current turn number to count against.
    /// </para>
    /// <para>
    /// Always positive when set, because an Asset whose Round has been reached is not returned at all. Null rather than zero when nothing is
    /// scheduled, since a client would read zero as "obsoleting this Round".
    /// </para>
    /// </remarks>
    public required int? ObsoleteInRounds { get; init; }

    /// <summary>
    /// How much of this Asset the Player holds, as a whole number, rounded down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolved from whichever store this Asset's <c>StorageScope</c> names, so the catalogue row stays a pure label:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>A banked Asset is summed across every bank the Player owns.</b> Two banks whose Settlements cannot reach each other are added together here on
    /// purpose, because that total is exactly what an Empire-wide expense is met from - see <c>docs/design/asset-banks.md</c>. A build order at one
    /// Settlement can only spend that Settlement's bank, which <c>GET /player/asset-banks/{settlementId}</c> reports.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>The Technology experience points are the Player's unspent directed research points</b>, held once per Player on
    /// <c>PlayerResearchState.UnspentDirectedPoints</c> and counted whole.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Culture reads zero</b>, because nothing stores culture points yet, and <b>every population figure reads zero</b>, because the holdings ledger and
    /// the roster reads they come from do not exist. Each is its own ticket.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Rounded down, so a Player holding nine tenths of a log reads as none of it - which is also all a drawdown would let them spend of it.
    /// </para>
    /// <para>
    /// <b>A banked Asset the Player holds none of is not returned at all</b>, so a zero on this field is never a banked figure: the toolbar would
    /// otherwise list every stone and log in the catalogue at zero. The other two scopes are returned at zero and do render - the Technology points
    /// empty at the end of every Turn by design, and the population figures read zero only because their ledger does not exist yet. See
    /// <c>GetPlayerAssetsHandler.EarnsATicker</c>.
    /// </para>
    /// </remarks>
    public required int Amount { get; init; }

    // -------------------------------------------------------------------------------------------------------------------------------------------------
    // Placeholders
    //
    // Nothing sources any of the six fields below yet and no table holds them. They are on the contract from the start so the ticker panel can be
    // wired against the final shape and the tickers render, rather than being omitted and changing the contract later. Each is filled in by its own
    // ticket. Until then a zero here is the absence of a source, not a measured value.
    // -------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The net change in the Player's holding each Turn, negative where consumption outruns production. <b>Placeholder - always zero</b>, awaiting the
    /// production and consumption ticket.
    /// </summary>
    public required int AmountAddedPerTurn { get; init; }

    /// <summary>
    /// Whether part of this Asset is held in a strategic reserve. <b>Placeholder - always false</b>, awaiting the strategic reserve ticket.
    /// </summary>
    public required bool HasStrategicReserve { get; init; }

    /// <summary>
    /// Whether the Player is importing this Asset. <b>Placeholder - always false</b>, awaiting the trade ticket.
    /// </summary>
    public required bool IsImported { get; init; }

    /// <summary>
    /// Whether the Player is exporting this Asset. <b>Placeholder - always false</b>, awaiting the trade ticket.
    /// </summary>
    public required bool IsExported { get; init; }

    /// <summary>
    /// Whether demand for this Asset is going unmet. <b>Placeholder - always false</b>, awaiting the demand ticket.
    /// </summary>
    public required bool DemandNotMet { get; init; }

    /// <summary>
    /// Whether this Asset currently counts as a luxury. <b>Placeholder - always false</b>, awaiting the luxury classification ticket.
    /// </summary>
    /// <remarks>
    /// The result of that classification, where <see cref="NeverLuxury"/> is one of its inputs.
    /// </remarks>
    public required bool IsLuxury { get; init; }
}
