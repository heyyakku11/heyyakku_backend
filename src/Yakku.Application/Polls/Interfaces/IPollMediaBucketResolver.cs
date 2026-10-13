using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IPollMediaBucketResolver
    {
        string Resolve(PollMediaType mediaType);
    }
}
