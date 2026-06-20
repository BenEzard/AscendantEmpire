using Microsoft.AspNetCore.Mvc;

namespace AscendantEmpire.Api.Utilities;

/// <summary>
/// Helpers for pulling the entered game/player out of the caller's game token on a controller.
/// </summary>
/// <remarks>
/// Game-scoped actions read the game and player from the token's claims, never from the request body or query string. 
/// Shared across controllers so the 403 "no game entered" behaviour stays identical.
/// </remarks>
public static class GameTokenExtensions
{
    /// <summary>
    /// Pulls the entered game id and player id out of the caller's game token. Returns false with a 403 <paramref name="error"/> result when the token has no game context 
    /// (i.e. the user hasn't entered a game via /auth/enter-game).
    /// </summary>
    public static bool TryGetGameContext(this ControllerBase controller, out int gameId, out int playerId, out IActionResult? error)
    {
        int? tokenGameId = controller.User.GetGameId();
        int? tokenPlayerId = controller.User.GetPlayerId();

        if (tokenGameId is null || tokenPlayerId is null)
        {
            gameId = 0;
            playerId = 0;
            error = new ObjectResult(new DTApiErrorResult("No game entered. Call /auth/enter-game first."))
            {
                StatusCode = 403,
            };
            return false;
        }

        gameId = tokenGameId.Value;
        playerId = tokenPlayerId.Value;
        error = null;
        return true;
    }
}
