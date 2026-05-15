namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;

/// <summary>
/// Immutable snapshot of values every possible-action check needs.
/// Avoids each check having to re-query game/player/turn basics.
/// </summary>
public sealed record PossibleActionContext
{
    public int GameId { get; init; }
    public int PlayerId { get; init; }
    public int CurrentRoundNumber { get; init; }

    public PossibleActionContext(int gameId, int playerId, int currentRoundNumber)
    {
        GameId = gameId;
        PlayerId = playerId;
        CurrentRoundNumber = currentRoundNumber;
    }

}