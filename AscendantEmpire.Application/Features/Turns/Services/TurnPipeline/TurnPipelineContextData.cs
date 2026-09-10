using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;

public sealed record TurnPipelineContextData
{
    public TurnPipelineType PipelineType { get; set; }
    public int GameId { get; init; }

    /// <summary>
    /// PlayerId is optional because some pipeline processes may be game-level and not specific to a single player's turn (i.e. Round) 
    /// </summary>
    public int? PlayerId { get; init; }

    public int CurrentRoundNumber { get; init; }

    /// <summary>
    /// The <c>turns.PlayerTurn</c> the pipeline is running inside, when the pipeline is Turn-scoped.
    /// </summary>
    /// <remarks>
    /// <c>Null</c> for the Round-scoped pipelines, which are not inside anybody's Turn.
    /// </remarks>
    public int? PlayerTurnId { get; init; }

    public TurnPipelineContextData(TurnPipelineType pipelineType, int gameId, int currentRoundNumber)
    {
        PipelineType = pipelineType;
        GameId = gameId;
        PlayerId = null;
        CurrentRoundNumber = currentRoundNumber;
    }

    public TurnPipelineContextData(TurnPipelineType pipelineType, int gameId, int playerId, int currentRoundNumber)
    {
        PipelineType = pipelineType;
        GameId = gameId;
        PlayerId = playerId;
        CurrentRoundNumber = currentRoundNumber;
    }
}
