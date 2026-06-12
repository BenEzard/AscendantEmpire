using System.Threading.Channels;

namespace AscendantEmpire.Application.BackgroundServices;

/// <summary>
/// In-process channel used to hand off background jobs from the API layer to the <see cref="BackgroundProcessingService"/>.
/// </summary>
/// <remarks>
/// Write the <c>ProcessingId</c> of a persisted <c>Processing</c> record to enqueue a job; the dispatcher reads the record, 
/// inspects its <c>ProcessType</c>, and routes it to the matching <see cref="IProcessExecutor"/>.
/// </remarks>
public interface IBackgroundProcessingChannel
{
    ChannelWriter<int> Writer { get; }
    ChannelReader<int> Reader { get; }
}
