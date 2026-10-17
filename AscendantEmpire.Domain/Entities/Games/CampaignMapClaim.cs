namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One Player's political claim on one campaign map tile. Sparse: a row exists only where a claim does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Claims are non-exclusive,</b> which is the whole reason they are a table rather than a column. Several Players may claim one tile at once, so there is no winner to
/// record on the tile and no winner column here either - the map read resolves what each Player sees. Ownership, being exclusive, does fit on the tile and lives on
/// <see cref="CampaignMap.OwnedByPlayerId"/>.
/// </para>
/// <para>
/// <b>Ownership trumps claim.</b> The two never coexist on one tile, so a tile becoming owned deletes every claim row on it. That is the ordinary way a border pushes
/// outwards when a Settlement is founded or a structure finishes.
/// </para>
/// <para>
/// <b>Keyed on the tile and the Player</b> rather than on an identity column, the same as <see cref="CampaignMapDepletion"/>: nothing points at a claim row, so an identity
/// would be a surrogate with no referrers and one more counter for the game delete to reseed. The composite key is also the uniqueness rule - one claim per Player per tile.
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="CampaignMapDepletion"/> and <c>PlayerKnownStructure</c>: claims are read and written in bulk by the per-round pass, over
/// an in-memory bitmap with deltas written back, never walked as an entity graph. See "How it is stored" in <c>docs/design/campaign-map/tile-ownership.md</c>.
/// </para>
/// </remarks>
public class CampaignMapClaim
{
    /// <summary>
    /// The campaign map tile being claimed. Half of the primary key.
    /// </summary>
    /// <remarks>
    /// The Player-neutral game tile, not a <c>PlayerCampaignMap</c> row: a claim is a fact about the world that other Players can come to learn, not a Player's view of it.
    /// </remarks>
    public int CampaignMapId { get; set; }

    /// <summary>
    /// The Player asserting the claim. The other half of the primary key.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The Game the tile and the Player belong to.
    /// </summary>
    /// <remarks>
    /// Implied by both key columns, but carried so the game delete can purge these rows in one statement rather than joining through the tiles - the same reasoning as
    /// <see cref="CampaignMapDepletion.GameId"/>.
    /// </remarks>
    public int GameId { get; set; }
}
