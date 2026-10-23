using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Interfaces;
using Yakku.Application.Storage;

namespace Yakku.Application.Sharing.Services
{
    public class SharedContentService : ISharedContentService
    {
        private const int MaxShareCodeLength = 64;

        private readonly IVotableContentRepository _contents;
        private readonly IObjectStorage _storage;
        private readonly IMediaUploadPolicy _policy;

        public SharedContentService(
            IVotableContentRepository contents,
            IObjectStorage storage,
            IMediaUploadPolicy policy)
        {
            _contents = contents;
            _storage = storage;
            _policy = policy;
        }

        public async Task<SharedContentResponse> GetAsync(
            string shareCode,
            VoterIdentity? viewer,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shareCode) || shareCode.Trim().Length > MaxShareCodeLength)
            {
                throw NotFound();
            }

            var content = await _contents.FindByShareCodeAsync(shareCode.Trim(), cancellationToken)
                ?? throw NotFound();

            return await BuildAsync(content, viewer, _contents, _storage, _policy, cancellationToken);
        }

        /// <summary>Shared with <see cref="VotingService"/>: the content as one caller sees it right now.</summary>
        internal static async Task<SharedContentResponse> BuildAsync(
            VotableContent content,
            VoterIdentity? viewer,
            IVotableContentRepository contents,
            IObjectStorage storage,
            IMediaUploadPolicy policy,
            CancellationToken cancellationToken)
        {
            var isOwner = viewer is { IsUser: true } && viewer.Id == content.OwnerId;
            if (!content.PubliclyReadable && !isOwner)
            {
                throw NotFound();
            }

            var tally = await contents.GetTallyAsync(content, cancellationToken);
            var myVote = viewer is null ? null : await contents.GetVoteAsync(content, viewer, cancellationToken);

            var response = SharedContentMapper.ToResponse(content, tally, myVote, viewer, DateTime.UtcNow);

            if (content.Poll?.Media is { } media && response.Poll?.Media is { } mediaResponse)
            {
                var download = await storage.CreateDownloadUrlAsync(
                    media.BucketName,
                    media.ObjectKey,
                    policy.DownloadUrlLifetime,
                    cancellationToken);
                mediaResponse.Url = download.Url;
                mediaResponse.UrlExpiresAt = download.ExpiresAt;
            }

            return response;
        }

        internal static AppException NotFound()
        {
            return new AppException(404, ApiErrorCodes.NotFound, "Content not found");
        }
    }
}
