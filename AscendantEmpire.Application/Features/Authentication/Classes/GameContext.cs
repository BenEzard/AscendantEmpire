namespace AscendantEmpire.Application.Features.Authentication.Classes;

/// <summary>
/// The user's identity together with the specific game they have entered and the Player
/// they control in it. Returned by the enter-game handler; the API layer turns this into a
/// signed game token so endpoints can read the game and player from the token.
/// </summary>
public record GameContext(int UserId, string Username, int GameId, int PlayerId);
