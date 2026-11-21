using Microsoft.Extensions.Options;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Caching;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
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
        private readonly IUserBlockLookup? _blocks;
        private readonly IResponseCache _cache;
        private readonly ResponseCacheOptions _cacheOptions;

        public SharedContentService(
            IVotableContentRepository contents,
            IObjectStorage storage,
            IMediaUploadPolicy policy,
            IUserBlockLookup? blocks = null,
            IResponseCache? cache = null,
            IOptions<ResponseCacheOptions>? cacheOptions = null)
        {
            _contents = contents;
            _storage = storage;
            _policy = policy;
            _blocks = blocks;
            _cache = cache ?? new NullResponseCache();
            _cacheOptions = cacheOptions?.Value ?? new ResponseCacheOptions();
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

            var code = shareCode.Trim();
            // Guests and users both have an id that affects MyVote / ResultsVisible.
            var viewerKey = ResponseCacheKeys.ViewerKey(viewer?.Id);
            var key = ResponseCacheKeys.ShareViewer(code, viewerKey);

            return await _cache.GetOrSetAsync(
                key,
                _cacheOptions.Share,
                async ct =>
                {
                    var content = await _contents.FindByShareCodeAsync(code, ct)
                        ?? throw NotFound();

                    return await BuildAsync(content, viewer, _contents, _storage, _policy, ct, _blocks);
                },
                cancellationToken);
        }

        public Task<SharedResultResponse> GetResultAsync(
            string shareCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shareCode) || shareCode.Trim().Length > MaxShareCodeLength)
            {
                throw NotFound();
            }

            var code = shareCode.Trim();
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.ShareResult(code),
                _cacheOptions.Share,
                async ct =>
                {
                    var content = await _contents.FindByShareCodeAsync(code, ct)
                        ?? throw NotFound();
                    if (string.Equals(content.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
                    {
                        throw NotFound();
                    }

                    var tally = await _contents.GetTallyAsync(content, ct);
                    return SharedContentMapper.ToResult(content, tally, DateTime.UtcNow);
                },
                cancellationToken);
        }

        /// <summary>Shared with <see cref="VotingService"/>: the content as one caller sees it right now.</summary>
        internal static async Task<SharedContentResponse> BuildAsync(
            VotableContent content,
            VoterIdentity? viewer,
            IVotableContentRepository contents,
            IObjectStorage storage,
            IMediaUploadPolicy policy,
            CancellationToken cancellationToken,
            IUserBlockLookup? blocks = null,
            MyVote? myVote = null)
        {
            var isOwner = viewer is { IsUser: true } && viewer.Id == content.OwnerId;
            if (!content.PubliclyReadable && !isOwner)
            {
                throw NotFound();
            }

            var tally = await contents.GetTallyAsync(content, cancellationToken);
            if (myVote is null && viewer is not null)
            {
                myVote = await contents.GetVoteAsync(content, viewer, cancellationToken);
            }

            var response = SharedContentMapper.ToResponse(content, tally, myVote, viewer, DateTime.UtcNow);

            // Someone the owner blocked sees the card as closed. They are never told it is because of a block.
            if (response.Voting.CanVote
                && viewer is { IsUser: true }
                && blocks is not null
                && await blocks.IsBlockedAsync(content.OwnerId, viewer.Id, cancellationToken))
            {
                response.Voting.CanVote = false;
                response.Voting.BlockedReason = content.IsPoll ? "CLOSED" : "INACTIVE";
            }

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
