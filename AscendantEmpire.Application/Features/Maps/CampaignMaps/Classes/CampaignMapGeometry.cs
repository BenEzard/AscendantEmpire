using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The shape of one Game's campaign map: its size, which edges join, and where its block of world-map tile ids starts.
/// </summary>
/// <remarks>
/// <para>
/// Everything the field-of-view rules need to know about the board itself, gathered into one value so the pure sight code never has to be handed four
/// loose arguments, or look anything up.
/// </para>
/// <para>
/// <see cref="CampaignMapOffsetId"/> is the <b>world</b> map's offset (<c>GameIndex.CampaignMapOffsetId</c>), never a Player's. The sight core works in
/// coordinates and only ever turns them into world-map ids, because terrain, Forces and Structures are all stored against the world map. A caller that
/// needs a Player's own map id translates for itself.
/// </para>
/// </remarks>
/// <param name="CampaignMapOffsetId">The world-map id of the tile at (0, 0).</param>
internal sealed record CampaignMapGeometry(int CampaignMapOffsetId, int Width, int Height, bool WrapsHorizontally, bool WrapsVertically)
{
    /// <summary>
    /// Reads the geometry off a Game's context.
    /// </summary>
    internal static CampaignMapGeometry From(GameContext game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return new CampaignMapGeometry(
            CampaignMapOffsetId: game.CampaignMapOffsetId,
            Width: game.MapWidth,
            Height: game.MapHeight,
            WrapsHorizontally: game.HasHorizontalWrapping,
            WrapsVertically: game.HasVerticalWrapping);
    }

    /// <summary>
    /// The world-map id of the tile at <paramref name="coord"/>.
    /// </summary>
    /// <remarks>
    /// Both maps are laid out row-major from their own offset, so an id is the offset plus the tile's position in the block. The coordinate must already
    /// be on the map; nothing here wraps it.
    /// </remarks>
    internal int GetCampaignMapId(Coord coord)
    {
        ArgumentNullException.ThrowIfNull(coord);

        return coord.GetDbId(CampaignMapOffsetId, Width);
    }

    /// <summary>
    /// The coordinate of the world-map tile with id <paramref name="campaignMapId"/> — the inverse of <see cref="GetCampaignMapId"/>.
    /// </summary>
    /// <remarks>
    /// Pure arithmetic over the row-major layout, so a read that only holds tile ids can place them without joining the map table. The id must belong to this Game's block.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The id falls outside this Game's block of tile ids.</exception>
    internal Coord GetCoord(int campaignMapId)
    {
        int positionInBlock = campaignMapId - CampaignMapOffsetId;

        // Refused rather than wrapped: an id from another Game's block would otherwise land on a plausible-looking tile of this one.
        if (positionInBlock < 0 || positionInBlock >= Width * Height)
        {
            throw new ArgumentOutOfRangeException(nameof(campaignMapId), campaignMapId, $"Tile id {campaignMapId} is not on a {Width} x {Height} map starting at id {CampaignMapOffsetId}.");
        }

        return new Coord(positionInBlock % Width, positionInBlock / Width);
    }

    /// <summary>
    /// How many tiles apart two coordinates are, taking the shorter way round any edge that wraps.
    /// </summary>
    internal int GetDistance(Coord from, Coord to)
    {
        return MapDirections.GetDistance(from, to, Width, Height, WrapsHorizontally, WrapsVertically);
    }

    /// <summary>
    /// The direction of a single step between two adjacent tiles, taking the shorter way round any edge that wraps.
    /// </summary>
    /// <returns><see langword="false"/> when the tiles are not neighbours, or are the same tile.</returns>
    internal bool TryGetStepDirection(Coord from, Coord to, out MoveDirection direction)
    {
        return MapDirections.TryGetStepDirection(from, to, Width, Height, WrapsHorizontally, WrapsVertically, out direction);
    }
}
