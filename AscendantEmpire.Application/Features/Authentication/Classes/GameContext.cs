using AscendantEmpire.Domain.Enums.Authentication;

namespace AscendantEmpire.Application.Features.Authentication.Classes;

/// <summary>
/// The user's identity together with the specific game they have entered and the Player
/// they control in it. Returned by the enter-game handler; the API layer turns this into a
/// signed game token so endpoints can read the game and player from the token.
/// </summary>
/// <remarks>
/// Permissions are granted to the account, not to the player, so they travel on the game token unchanged — entering a game neither grants nor removes any.
/// </remarks>
public record GameContext(int UserId, string Username, int GameId, int PlayerId, IReadOnlyCollection<PermissionType> Permissions);
