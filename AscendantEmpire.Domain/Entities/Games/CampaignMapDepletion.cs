namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// How much Forage, Hunt, Fish and Fodder has been taken from one campaign map tile, by anyone, and the last Round a Force fed on it.
/// </summary>
/// <remarks>
/// <para>
/// Player-neutral. The tile stores what has been <b>taken</b>, never what remains, because Players with different sustenance values reach different amounts on the same
/// tile: what is available to a Player is <c>MAX(0, [Player's effective value] − Taken)</c>. A Player gaining or losing a modifier just sees a different amount against the
/// same <c>Taken</c>, and no Player is recorded here. See "What a tile stores: the amount taken" in <c>docs/design/campaign-map.md</c>.
/// </para>
/// <para>
/// Only tiles that have been fed on have a row: it is created the first time a Force feeds on the tile, and persists between Turns. A tile with no row has had nothing
/// taken and has never been fed on. A Player's view joins to it on <c>PlayerCampaignMap.CampaignMapId</c>.
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="PlayerSustenanceModifier"/>: the rows are read and written by tile id, never walked as an entity graph.
/// </para>
/// </remarks>
public class CampaignMapDepletion
{
    /// <summary>
    /// The campaign map tile. Also the primary key: a tile has at most one depletion row.
    /// </summary>
    /// <remarks>
    /// The Player-neutral game tile, not a <c>PlayerCampaignMap</c> row, since what is taken from a tile is shared by every Player.
    /// </remarks>
    public int CampaignMapId { get; set; }

    /// <summary>
    /// The Game the tile belongs to.
    /// </summary>
    /// <remarks>
    /// Implied by <see cref="CampaignMapId"/>, but carried so the game delete can purge these rows directly rather than through the tiles.
    /// </remarks>
    public int GameId { get; set; }

    /// <summary>
    /// Forage taken from the tile, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    public long ForageTaken { get; set; }

    /// <summary>
    /// Hunt taken from the tile, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    public long HuntTaken { get; set; }

    /// <summary>
    /// Fish taken from the tile, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    public long FishTaken { get; set; }

    /// <summary>
    /// Fodder taken from the tile, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    /// <remarks>
    /// Always 0 for now: fodder feeds animals, and nothing extracts it until mounted units have food requirements.
    /// </remarks>
    public long FodderTaken { get; set; }

    /// <summary>
    /// The last Round a Force fed on the tile, or <see langword="null"/> when none ever has.
    /// </summary>
    /// <remarks>
    /// "Fed on this Round" is <c>FedOnRound == CurrentRoundNumber</c>, so the marker never needs resetting. It records presence, not food: it is set when a Force ends its
    /// Turn on the tile whether or not there was anything left to take. Regeneration reads it.
    /// </remarks>
    public int? FedOnRound { get; set; }
}
