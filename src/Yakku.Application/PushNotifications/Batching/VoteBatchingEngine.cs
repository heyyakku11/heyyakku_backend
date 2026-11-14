using Yakku.Domain.Enums;

namespace Yakku.Application.PushNotifications.Batching
{
    /// <summary>The result of one scheduled evaluation of a batched card.</summary>
    public sealed record EvaluationOutcome(VoteDeliveryMode Mode, bool ModeChanged, int Frequency);

    /// <summary>
    /// Registers a vote in the card's rolling window, decides how it is delivered and records that decision, as a
    /// loop of "read atomically, decide in <see cref="VoteBatchingPolicy"/>, write if nothing changed meanwhile".
    /// Every step is idempotent, so a vote processed twice (a retry after a crash) is counted once.
    /// </summary>
    public sealed class VoteBatchingEngine
    {
        private const int MaxAttempts = 5;

        private readonly IVoteBatchingStore _store;
        private readonly VoteBatchingPolicy _policy;

        public VoteBatchingEngine(IVoteBatchingStore store, VoteBatchingPolicy policy)
        {
            _store = store;
            _policy = policy;
        }

        public async Task<VoteDecision> DecideAsync(
            string cardKey,
            Guid eventId,
            DateTime eventAt,
            VoteDeliveryMode durableMode,
            IReadOnlyList<CompanionVote> companions,
            CancellationToken cancellationToken = default)
        {
            var at = UnixMillis.From(eventAt);
            var addedByThisCall = false;
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var snapshot = await _store.RegisterAsync(
                    new RegisterVoteRequest(cardKey, eventId, at, durableMode, companions),
                    cancellationToken);

                // After a lost compare-and-set the vote is already counted, by this very call, but it has not been
                // written to the batch yet. Seeing it again is not a duplicate of an earlier, finished attempt.
                if (snapshot.Added)
                {
                    addedByThisCall = true;
                }
                else if (addedByThisCall && snapshot.Duplicate)
                {
                    snapshot = snapshot with { Added = true };
                }

                var plan = _policy.PlanVote(snapshot, cardKey, at, Guid.NewGuid());
                if (plan.Apply is null)
                {
                    return plan.Decision;
                }

                var result = await _store.ApplyAsync(plan.Apply, cancellationToken);
                if (result.Applied)
                {
                    return plan.Decision with
                    {
                        Batch = plan.Decision.Batch! with
                        {
                            Generation = result.Generation,
                            EventCount = result.PendingCount
                        }
                    };
                }

                // Another vote of the card (or a flush) changed its state first. Registering again is harmless.
            }

            throw new VoteBatchingConflictException(cardKey);
        }

        /// <summary>One hysteresis evaluation: counts the card's current window and moves its low-frequency count.</summary>
        public async Task<EvaluationOutcome> EvaluateAsync(
            string cardKey,
            VoteDeliveryMode durableMode,
            CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var snapshot = await _store.RegisterAsync(
                    new RegisterVoteRequest(cardKey, null, 0, durableMode, []),
                    cancellationToken);
                var plan = _policy.PlanEvaluation(snapshot, cardKey);
                var result = await _store.ApplyAsync(plan.Apply, cancellationToken);
                if (result.Applied)
                {
                    return new EvaluationOutcome(plan.Mode, plan.ModeChanged, snapshot.Frequency);
                }
            }

            throw new VoteBatchingConflictException(cardKey);
        }
    }
}
