namespace Yakku.Application.AskBack.Interfaces
{
    /// <summary>
    /// Checks, before a top-up order is created, that the caller may add questions to the conversation. Implemented by
    /// the ask back service, which owns the conversation rules.
    /// </summary>
    public interface IConversationTopUpGate
    {
        /// <summary>
        /// Returns when the conversation can be topped up by <paramref name="userId"/>. Throws 409
        /// <c>ASK_BACK_TOP_UP_NOT_AVAILABLE</c> with the reason otherwise, and 404 when the caller is not a participant.
        /// </summary>
        Task EnsureCanTopUpAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken = default);
    }
}
