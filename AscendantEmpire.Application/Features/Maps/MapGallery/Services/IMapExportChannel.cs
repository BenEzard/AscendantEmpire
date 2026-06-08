using System.Threading.Channels;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Services;

/// <summary>
/// In-process channel used to hand off export jobs from the API layer to the
/// <c>MapExportBackgroundService</c>. Write the <c>ProcessingId</c> to enqueue a job.
/// </summary>
public interface IMapExportChannel
{
    ChannelWriter<int> Writer { get; }
    ChannelReader<int> Reader { get; }
}
