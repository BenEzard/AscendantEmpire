namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// A MapResource instance a Player has discovered.
/// </summary>
/// <remarks>
/// <para>
/// Discovered is not the same as seen, and the two decide different things. A row here means the Player is shown the MapResource on the tile from then on, whether or not
/// they are watching it. Whether they are watching it is derived live from their Observers' vision and never stored, the same as sustenance, and decides only whether its
/// estimated quantity comes with it. See "What a discovered MapResource shows" in <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// An instance created by spread is discovered at once by every Player who had discovered the instance it spread from, so these rows are copied when it spreads, and
/// deleted with the instance when it is gone for good.
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="PlayerKnownStructure"/>.
/// </para>
/// </remarks>
public class PlayerKnownMapResource
{
    public int Id { get; set; }

    /// <summary>
    /// The Game, carried so the game delete can purge these rows directly.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// The Player who discovered the instance.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The instance discovered.
    /// </summary>
    public int MapResourceInstanceId { get; set; }

    /// <summary>
    /// The Round the Player discovered it, or the Round it spread in for an instance discovered through its parent.
    /// </summary>
    public int DiscoveredOnRound { get; set; }

    /// <summary>
    /// The deepest layer of a stratified instance the Player has surveyed, or <see langword="null"/> for any other kind.
    /// </summary>
    /// <remarks>
    /// Layers are surveyed in order, so every layer down to this one is known. A survey can reveal that a layer is empty, and that still counts.
    /// </remarks>
    public int? DeepestLayerSurveyed { get; set; }
}
