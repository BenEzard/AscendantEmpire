namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The ownership columns of one Campaign Map tile, as the database holds them.
/// </summary>
/// <remarks>
/// <para>
/// The input to <see cref="TileOwnershipDerivation"/>, and the seam that keeps it pure: the reader projects these columns off <c>game.CampaignMap</c> 
/// and the rule that turns them into a state never touches a context. 
/// </para>
/// <para>
/// <c>AbandonedOnTurnId</c> is deliberately absent. It is set and cleared with <see cref="LastOwnedByPlayerId"/>, so the Player reference alone answers 
/// whether the tile is abandoned, and the turn stamp only matters to the per-round pass that counts the revert down.
/// </para>
/// </remarks>
internal sealed record TileOwnershipColumns
{
    /// <summary>The world-map id of the tile these columns came from.</summary>
    public required int CampaignMapId { get; init; }

    /// <inheritdoc cref="Domain.Entities.Games.CampaignMap.OwnedByPlayerId"/>
    public int? OwnedByPlayerId { get; init; }

    /// <inheritdoc cref="Domain.Entities.Games.CampaignMap.OccupiedByPlayerId"/>
    public int? OccupiedByPlayerId { get; init; }

    /// <inheritdoc cref="Domain.Entities.Games.CampaignMap.LastOwnedByPlayerId"/>
    public int? LastOwnedByPlayerId { get; init; }

    /// <inheritdoc cref="Domain.Entities.Games.CampaignMap.RebellionPercentRemaining"/>
    public int? RebellionPercentRemaining { get; init; }

    /// <inheritdoc cref="Domain.Entities.Games.CampaignMap.HasClaims"/>
    public bool HasClaims { get; init; }
}
