using Microsoft.Extensions.Logging;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;

namespace Yakku.Infrastructure.Push
{
    /// <summary>What a send did. <paramref name="MessageId"/> is the provider's id when a device accepted the push.</summary>
    public readonly record struct PushSendResult(PushOutcome Outcome, string? MessageId);

    /// <summary>Sends one vote push to the card owner's devices and turns the result into a <see cref="PushOutcome"/>.</summary>
    public sealed class VotePushSender
    {
        private readonly IPushNotificationService _push;
        private readonly ILogger<VotePushSender> _logger;

        public VotePushSender(IPushNotificationService push, ILogger<VotePushSender> logger)
        {
            _push = push;
            _logger = logger;
        }

        public async Task<PushSendResult> SendAsync(
            Guid ownerId,
            NotificationPayload payload,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _push.SendToUserAsync(ownerId, payload, cancellationToken);
                var outcome = VoteNotificationDispatcher.Classify(result, null);
                return new PushSendResult(outcome, outcome == PushOutcome.Submitted ? result.FirebaseMessageId : null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var outcome = VoteNotificationDispatcher.Classify(null, exception);
                if (outcome == PushOutcome.Retry)
                {
                    _logger.LogWarning(exception, "Vote push failed for owner {OwnerId}.", ownerId);
                }
                else
                {
                    _logger.LogInformation("No push-eligible device for owner {OwnerId}; skipping vote push.", ownerId);
                }

                return new PushSendResult(outcome, null);
            }
        }
    }
}
