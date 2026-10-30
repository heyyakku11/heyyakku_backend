using Yakku.Application.AskBack.DTOs;

namespace Yakku.Application.AskBack.Interfaces
{
    public interface IAskBackService
    {
        /// <summary>Creates the conversation for a vote, or returns the one that already exists.</summary>
        Task<(AskBackConversationResponse Conversation, bool Created)> CreateConversationAsync(
            Guid userId,
            CreateAskBackConversationRequest request,
            CancellationToken cancellationToken = default);

        Task<AskBackPage<AskBackConversationListItemResponse>> ListConversationsAsync(
            Guid userId,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken = default);

        Task<AskBackConversationResponse> GetConversationAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default);

        Task<AskBackPage<AskBackMessageResponse>> ListMessagesAsync(
            Guid userId,
            Guid conversationId,
            string? cursor,
            string? direction,
            int? limit,
            CancellationToken cancellationToken = default);

        Task<AskBackMessageResponse> SendMessageAsync(
            Guid userId,
            Guid conversationId,
            SendAskBackMessageRequest request,
            CancellationToken cancellationToken = default);

        Task<AskBackReadResponse> MarkReadAsync(
            Guid userId,
            Guid conversationId,
            MarkAskBackReadRequest request,
            CancellationToken cancellationToken = default);

        Task<AskBackConversationResponse> SetMutedAsync(
            Guid userId,
            Guid conversationId,
            bool muted,
            CancellationToken cancellationToken = default);

        Task<AskBackConversationResponse> CloseAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default);

        Task<AskBackConversationResponse> ReopenAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default);

        Task<AskBackConversationResponse> BlockAsync(
            Guid userId,
            Guid conversationId,
            BlockAskBackRequest request,
            CancellationToken cancellationToken = default);

        Task<AskBackConversationResponse> UnblockAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default);

        /// <summary>Whether the user may join the conversation's real-time group. Used by the hub.</summary>
        Task<bool> CanJoinAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default);
    }
}
