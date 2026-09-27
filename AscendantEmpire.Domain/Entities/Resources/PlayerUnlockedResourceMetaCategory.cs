using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Resources;

/// <summary>
/// Records that a Player has unlocked a particular <see cref="ResourceMetaCategory"/>, and when it stops being available to them.
/// </summary>
/// <remarks>
/// <para>
/// This is what decides whether the meta-category's icon appears in the game's Resources toolbar group at all: no row, no icon. Rows are populated by the
/// (future) Technology system - "Construction Materials" appearing once one of its precedent technologies is discovered - and until then by a new game's starter
/// set and the development-only unlock endpoint. Composite primary key on (PlayerId, ResourceMetaCategoryId).
/// </para>
/// <para>
/// The categories and groups beneath a meta-category have no unlock rows of their own. They are shown when they hold at least one available Resource and hidden
/// when they do not, so their visibility cannot disagree with their contents. An unlocked meta-category with nothing available under it still shows its icon,
/// over an empty panel: hiding something the Player has been granted would be stranger than showing it empty.
/// </para>
/// <para>
/// <b><see cref="ObsoleteOnRound"/> is future-dated</b>, on the same terms as <see cref="PlayerUnlockedResource.ObsoleteOnRound"/> - see the remarks there.
/// </para>
/// </remarks>
public class PlayerUnlockedResourceMetaCategory
{
    public int PlayerId { get; set; }

    public int ResourceMetaCategoryId { get; set; }

    /// <summary>
    /// The Round on which this meta-category became unlocked for the Player.
    /// </summary>
    public int UnlockedOnRound { get; set; }

    /// <summary>
    /// The Round on which this meta-category stops being available to the Player, or <see langword="null"/> while it is not scheduled to.
    /// </summary>
    public int? ObsoleteOnRound { get; set; }

    public Player Player { get; set; } = null!;

    public ResourceMetaCategory MetaCategory { get; set; } = null!;
}
