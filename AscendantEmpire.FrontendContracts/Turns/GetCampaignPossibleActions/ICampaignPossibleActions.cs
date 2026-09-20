using AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

namespace AscendantEmpire.FrontendContracts.Turns.GetCampaignPossibleActions;

/// <summary>
/// What a Player still has outstanding on the Campaign Turn that is open, read without touching it.
/// </summary>
/// <remarks>
/// <para>
/// The same list that comes back from an end-turn check, on a route that only reads: a client showing it as the Player opens their Game must not be
/// able to end their Turn by asking.
/// </para>
/// </remarks>
public interface ICampaignPossibleActions
{
    /// <summary>
    /// Whether the open Campaign Turn is the calling Player's.
    /// </summary>
    /// <remarks>
    /// <para>False is an ordinary answer, not an error: it covers another Player's Turn and a Game with no Turn open at all. It exists so a client can
    /// tell "nothing is outstanding" from "you are not the one being asked", which read the same in <see cref="PossibleActions"/>.</para>
    /// </remarks>
    public bool IsYourTurn { get; }

    /// <summary>
    /// What the Player could still do before ending their Turn. Always empty when <see cref="IsYourTurn"/> is false.
    /// </summary>
    public IReadOnlyCollection<IPossibleAction> PossibleActions { get; }
}
