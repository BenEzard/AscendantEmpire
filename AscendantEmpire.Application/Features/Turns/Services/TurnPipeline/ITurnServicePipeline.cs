using AscendantEmpire.Application.Common;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;

internal interface ITurnServicePipeline<TPipelineProcessInterface>
    where TPipelineProcessInterface : IPipelineProcess
{
    Task<ServiceResultBase> ProcessPipelineAsync(TurnPipelineContextData context);
}