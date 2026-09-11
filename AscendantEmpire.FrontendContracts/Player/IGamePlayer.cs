using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.FrontendContracts.Player;

/// <summary>
/// One player in the game the caller has entered, as any other player in it may see them.
/// </summary>
/// <remarks>
/// Public identity only — who they are and what colour they fly — so the client can name the owner of a force in an intelligence report, or draw a faction in
/// its colour. Nothing here is private to the player it describes.
/// </remarks>
public interface IGamePlayer
{
    int PlayerId { get; }

    /// <summary>
    /// "Title Name (Faction)", the same shape the Open Game listing uses.
    /// </summary>
    string RulerLabel { get; }

    string FactionName { get; }

    /// <summary>
    /// The faction's HTML colour, or null while the player has not picked one.
    /// </summary>
    string? FactionColour { get; }

    PlayerStatus Status { get; }
}
