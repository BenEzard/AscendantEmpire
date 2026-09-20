namespace AscendantEmpire.Application.Features.Turns.Services;

/// <summary>
/// Confirms a Player may act on a Game's Campaign Turn Queue, and hands back the Turn that is open.
/// </summary>
/// <remarks>
/// <para>
/// The two checks every Campaign Turn request repeats — the Game is in progress with this Player active in it, and a Turn is actually open — in one
/// place, so ending a Turn and reading what is outstanding on it cannot drift apart.
/// </para>
/// <para>
/// Deliberately stops short of deciding whose Turn it is. That judgement differs by caller and is made against the returned Turn.
/// </para>
/// </remarks>
internal interface ICampaignTurnAccessService
{
    /// <summary>
    /// Validates the Game and Player, then reads the Game's current Campaign Turn.
    /// </summary>
    /// <param name="gameId">The Game whose Campaign Turn Queue is being read.</param>
    /// <param name="playerId">The Player making the request, whose membership of the Game is checked.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    Task<CampaignTurnAccess> GetCampaignTurnAccessAsync(int gameId, int playerId, CancellationToken cancelToken);
}
