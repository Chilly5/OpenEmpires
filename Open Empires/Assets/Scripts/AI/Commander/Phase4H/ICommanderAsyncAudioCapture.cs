using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    /// <summary>Optional asynchronous finalization for browser audio that loads after stop.
    /// Stop owns final-tail collection; cancellation must invalidate pending buffers.</summary>
    public interface ICommanderAsyncAudioCapture
    {
        Task<CommanderAudioData> StopRecordingAsync(CancellationToken cancellationToken);
    }
}
