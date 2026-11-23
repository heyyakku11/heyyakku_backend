using System.Threading.Channels;
using Yakku.Application.Auth.Sms;

namespace Yakku.Infrastructure.Sms
{
    public sealed class OtpSmsQueue : IOtpSmsQueue
    {
        private readonly ChannelWriter<SmsOtpJob> _writer;

        public OtpSmsQueue(Channel<SmsOtpJob> channel)
        {
            _writer = channel.Writer;
        }

        public ValueTask EnqueueAsync(SmsOtpJob job, CancellationToken cancellationToken = default)
        {
            return _writer.WriteAsync(job, cancellationToken);
        }
    }
}
