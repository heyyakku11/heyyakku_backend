using System.Threading.Channels;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;

namespace Yakku.Infrastructure.Email
{
    public sealed class AdminEmailQueue : IAdminEmailQueue
    {
        private readonly ChannelWriter<AdminEmailJob> _writer;

        public AdminEmailQueue(Channel<AdminEmailJob> channel)
        {
            _writer = channel.Writer;
        }

        public ValueTask EnqueueAsync(AdminEmailJob job, CancellationToken cancellationToken = default)
        {
            return _writer.WriteAsync(job, cancellationToken);
        }
    }
}
