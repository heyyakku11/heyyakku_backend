using System.Threading.Channels;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;

namespace Yakku.Infrastructure.Email
{
    public sealed class OtpEmailQueue : IOtpEmailQueue
    {
        private readonly ChannelWriter<EmailJob> _writer;

        public OtpEmailQueue(Channel<EmailJob> channel)
        {
            _writer = channel.Writer;
        }

        public ValueTask EnqueueAsync(EmailJob job, CancellationToken cancellationToken = default)
        {
            return _writer.WriteAsync(job, cancellationToken);
        }
    }
}
