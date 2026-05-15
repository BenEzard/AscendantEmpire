using AscendantEmpire.Application.Common;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;

internal class TurnServicePipeline<TPipelineProcessInterface>(
    IEnumerable<TPipelineProcessInterface> pipelineItems,
    ILogger<TurnServicePipeline<TPipelineProcessInterface>> logger) : ITurnServicePipeline<TPipelineProcessInterface>
    where TPipelineProcessInterface : IPipelineProcess
{
    public async Task<ServiceResultBase> ProcessPipelineAsync(TurnPipelineContextData context)
    {
        var result = Succeed();

        // Execute checks in defined order so that expensive checks can be placed last
        IPipelineProcess[] orderedPipelineItems = [.. pipelineItems.OrderBy(c => c.ExecutionOrder)];
        logger.LogWarning("Starting {Type} pipeline: {Count} items.", context.PipelineType, orderedPipelineItems.Length);
        foreach (IPipelineProcess processItem in orderedPipelineItems)
        {
            try
            {
                string itemError = await processItem.ProcessAsync(context);

                if (!string.IsNullOrEmpty(itemError))
                {
                    logger.LogWarning("While processing '{ItemName}' for PlayerId {PlayerId}, GameId {GameId}, encountered error: {Error}",
                        processItem.ItemName, context.PlayerId, context.GameId, itemError);

                    return ServiceResult.Fail(itemError);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Possible action check '{ItemName}' threw an exception for PlayerId {PlayerId}, GameId {GameId}.",
                    processItem.ItemName, context.PlayerId, context.GameId);

                return InternalServerError($"An unexpected error occurred while processing '{processItem.ItemName} (Exception: {ex}).");
            }
        }
        logger.LogWarning("Successfully processed {Type} pipeline: {Count} items.", context.PipelineType, orderedPipelineItems.Length);

        return result;
    }
}