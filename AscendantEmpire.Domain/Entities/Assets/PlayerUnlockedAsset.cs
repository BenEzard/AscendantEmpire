using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Records that a Player has unlocked a particular <see cref="Asset"/>, and when it stops being available to them.
/// </summary>
/// <remarks>
/// <para>
/// The presence of a row means the Asset is unlocked for that Player; no row means they have never had it. Rows are populated by the (future) Technology
/// system, and until then by a new game's starter set and the development-only unlock endpoint. Composite primary key on (PlayerId, AssetId).
/// </para>
/// <para>
/// <b><see cref="ObsoleteOnRound"/> is future-dated.</b> It is the Round availability <i>ends</i>, not a marker that it already has, so an Asset is available
/// while the value is <see langword="null"/> <i>or</i> still ahead of the current Round. That is what lets the UI say "obsoleting in N turns" rather than only
/// "gone". Note this differs from <c>PlayerUnlockedComponent.ObsoleteOnTurn</c>, which its readers treat as a plain "already obsolete" flag — an availability
/// check must not be copied across from the unit-design handlers.
/// </para>
/// <para>
/// Rounds, not Turns: the game clock is <c>TurnQueue.CurrentRoundNumber</c>, and there is no current turn number to compare a Turn against.
/// </para>
/// </remarks>
public class PlayerUnlockedAsset
{
    public int PlayerId { get; set; }

    public int AssetId { get; set; }

    /// <summary>
    /// The Round on which this Asset became unlocked for the Player.
    /// </summary>
    public int UnlockedOnRound { get; set; }

    /// <summary>
    /// The Round on which this Asset stops being available to the Player, or <see langword="null"/> while it is not scheduled to.
    /// </summary>
    /// <remarks>
    /// A Round still in the future means the Asset is available and obsoleting - the UI counts the difference down. Once the current Round reaches it, the
    /// Asset drops out of view.
    /// </remarks>
    public int? ObsoleteOnRound { get; set; }

    public Player Player { get; set; } = null!;

    public Asset Asset { get; set; } = null!;
}
