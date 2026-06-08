using System.Threading.Channels;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Services;

/// <summary>
/// Singleton bounded channel implementation. A capacity of 100 is generous given
/// map exports are user-initiated one-at-a-time operations.
/// </summary>
public sealed class MapExportChannel : IMapExportChannel
{
    private readonly Channel<int> _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
    {
        // Drop oldest if somehow the queue fills up; callers should not enqueue more than one job at a time.
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

    public ChannelWriter<int> Writer => _channel.Writer;
    public ChannelReader<int> Reader => _channel.Reader;
}
