namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;

internal interface IPipelineProcess
{
    /// <summary>
    /// Descriptive name for logging/diagnostics.
    /// </summary>
    string ItemName { get; }

    /// <summary>
    /// Lower values run first.
    /// </summary>
    int ExecutionOrder { get; }

    /// <summary>
    /// Process items from the pipeline.
    /// </summary>
    Task<string> ProcessAsync(TurnPipelineContextData context);
}
