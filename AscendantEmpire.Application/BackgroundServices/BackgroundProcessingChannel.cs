using System.Threading.Channels;

using AscendantEmpire.Application.Options;

using Microsoft.Extensions.Options;

namespace AscendantEmpire.Application.BackgroundServices;

/// <summary>
/// Singleton bounded channel shared between the handlers that enqueue jobs and the dispatcher that drains them.
/// </summary>
/// <remarks>
/// The capacity is read from <see cref="BackgroundProcessingSettings.ChannelCapacity"/>. 
/// It is a backpressure ceiling, not a concurrency limit — background jobs (map export / generation) are user-initiated and 
/// processed one at a time.
/// </remarks>
public sealed class BackgroundProcessingChannel : IBackgroundProcessingChannel
{
    private readonly Channel<int> _channel;

    public BackgroundProcessingChannel(IOptions<BackgroundProcessingSettings> options)
    {
        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(options.Value.ChannelCapacity)
        {
            // Block the writer if the queue ever fills up rather than dropping jobs.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public ChannelWriter<int> Writer => _channel.Writer;
    public ChannelReader<int> Reader => _channel.Reader;
}
