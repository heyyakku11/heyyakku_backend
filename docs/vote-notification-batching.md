# Vote notifications: frequency-based batching

When someone votes on a card (a poll or a check), its owner gets a push. A card that is voted on quickly would flood
the owner, so the push strategy follows how fast **that card** is being voted on. The earlier fixed 10-minute digest
window is gone.

## Rules

| Rule | Value (all configurable) |
| --- | --- |
| The card's first votes | The first **3** votes are always pushed one by one, whatever the frequency. |
| Frequency | Votes of the card in a rolling **60 s** window. |
| Individual mode | 0-2 votes in the window: each vote gets its own push. |
| Batch mode | 3 or more votes in the window: votes are collected into one push per batch. |
| Quiet period | 3-5 votes: **15 s**, 6-15 votes: **10 s**, 16 or more: **5 s** after the last vote. |
| Maximum wait | A batch is sent **30 s** after its first vote at the latest, however long votes keep coming. |
| Back to individual | After **2 consecutive** evaluation periods (60 s each) with at most 2 votes in the window. |

A batch is due at `MIN(LastVoteAt + QuietPeriod, FirstPendingAt + MaximumBatchWait)`. The cap is measured from the
first pending vote and never moves, so continuous voting cannot postpone a batch indefinitely.

Every card has its own window, mode, batch and evaluation schedule. Nothing is shared between cards: there is no global
voting-frequency counter.

The push for a batch reads **"Your card is getting attention!"** / "N people have voted on your card. Tap to see the
results." and opens the card's results. Like every push it carries no voter, answer or card text.

## Which cards

Polls, checks and personality cards all use this pipeline. A personality card is recorded the same way, in the vote's
transaction (`PersonalityService.VoteAsync`). Its completing vote (the card needs 5) is the exception: it sends only
the "Who you are is in" push, not a vote push as well. Votes 1-4 notify the owner; the inbox line reads "N of 5
friends". A push opens `yakku://results/personality/{cardId}`.

## How a vote flows

1. **In the vote's transaction** (`VoteNotificationOutbox`): one atomic upsert numbers the vote on its card
   (`vote_notification_cards.vote_count`). Votes 1-3 are stored as `IMMEDIATE` events, later ones as `UNDECIDED`. Redis
   is not touched, so a vote never waits on it and the first-three rule never depends on it.
2. **Classification** (`VoteEventClassifier`, after the commit): claims `UNDECIDED` events with
   `FOR UPDATE SKIP LOCKED`, registers each in the card's Redis window, and decides from that card's own frequency:
   - individual: the event becomes `IMMEDIATE` and is sent by `VoteNotificationDispatcher`;
   - batch: the event is attached to the card's open batch (`vote_digest_windows`, one row per batch), or a new batch
     is opened.
3. **Flush** (`VoteBatchDispatcher`): Redis hands out batches as they become due (polled every second while a batch
   waits; every `IdleRedisPollSeconds` otherwise). A database sweep takes whatever Redis could not.
   Order: claim in the database (compare-and-set on status and lease token), close the batch in Redis (later votes start
   a new batch), count the events still eligible (not read before the batch's cutoff), send, mark sent.
4. **Hysteresis** (`CardModeEvaluator`): a batched card is re-evaluated every 60 s and leaves batch mode only after
   2 consecutive low-frequency evaluations.

## Where state lives

PostgreSQL is the source of truth. Redis is rebuildable coordination state.

| Redis key | Type | Holds |
| --- | --- | --- |
| `yakku:notification:card:{card}:votes` | ZSET | event ids of the card's votes, scored by time (rolling window) |
| `yakku:notification:card:{card}:state` | HASH | delivery mode, state version, low-frequency periods |
| `yakku:notification:card:{card}:pending` | HASH | the open batch: id, count (advisory), first/last vote, due time, cap, generation |
| `yakku:notification:flush:queue` | ZSET | cards with an open batch, scored by due time |
| `yakku:notification:evaluate:queue` | ZSET | batched cards, scored by next evaluation time |

`{card}` is `poll:{id}` or `check:{id}`. Keys expire (`StateTtlSeconds`, default 24 h). Atomic steps are small Lua
scripts; every decision is made in C# (`VoteBatchingPolicy`) and written back with a compare-and-set on the state
version and batch generation. Time comes from Redis `TIME`, so instances with different clocks agree.

PostgreSQL tables: `vote_notification_cards` (counter, `delivery_mode`, `last_vote_at`), `vote_notification_events`
(one row per vote: kind `IMMEDIATE`/`DIGEST`/`UNDECIDED`, status, attempts, lease, provider message id) and
`vote_digest_windows` (one row per batch; the name predates the batching. `id` is the batch id and the push's
`deliveryId`, `starts_at` is the first pending vote, `ends_at` the due time, `max_flush_at` the cap, `generation` the
reschedule counter).

## Delivery guarantees

- **At least once. Not exactly once.** A crash after the push provider accepted a push and before the row is marked sent
  makes the push go out again. Every push carries a stable `deliveryId` (the event id, or the batch id) that is the same
  on every retry, so the app can drop a duplicate.
- A batch or event is marked sent only after the provider accepted the push.
- Retries: 5 s, 30 s, 2 min, then `FAILED` (`MaxAttempts` = 4 sends in total). A failed batch stays closed; new votes go to a new batch.
- Votes arriving while a batch is being sent go to a new batch.
- `PendingEventCount` in Redis is advisory. The count in the push comes from the database at flush time.

## Failure behaviour

| Failure | Effect |
| --- | --- |
| Redis down | Votes stay `UNDECIDED` in PostgreSQL and are retried every 5 s. After `RedisFallbackAfterSeconds` (120 s) a vote joins a database-only batch that the sweep sends, so a Redis outage delays pushes by a bounded time. Nothing is lost. |
| Redis loses its data | The sweep sends open batches `OverdueGraceSeconds` after their due time. The card's frequency window restarts from the next votes (the classifier seeds it with up to 20 earlier votes of the card from PostgreSQL), so the mode may briefly be lower than the true frequency. A leftover open batch is handed to the sender when the card opens a new one. |
| Worker dies mid-send | The claim lease (`SendLeaseSeconds`) runs out and another worker sends it. |
| Two workers claim the same batch | The conditional update lets one win; the other skips it. |
| Batch in Redis but never in the database | Dropped after `OrphanedPendingSeconds` (300 s). |

## Configuration

Section `VoteNotifications` in `appsettings.json`, or environment variables `VoteNotifications__Name`
(`VoteNotifications__QuietPeriods__0__MinVotesPerMinute`, ...). Values are validated when the application starts and
a bad value stops the start with every problem listed. Defaults are in `appsettings.json` and
`VoteNotificationBatchingOptions`.

Notable constraints: `IndividualThreshold` must be below `BatchThreshold`; quiet periods must increase with the
threshold and not exceed `MaximumBatchWaitSeconds`; batch sizes are capped at 1000.

## Observability

Metrics on the `Yakku.VoteNotifications` meter (`System.Diagnostics.Metrics`; read them with OpenTelemetry or
`dotnet-counters`): votes per minute, batch size, flush delay, delivery latency (from the vote to the provider
accepting; take p50/p95/p99 in your metrics backend), classification duration, events classified, batches created and
flushed by result, individual pushes, mode changes, duplicates, retries, push failures (final or not), Redis failures
and fallbacks, claim conflicts, flush queue size and due-but-waiting batches. No metric or log carries a token, phone
number or voter identity.

## Deployment

1. The migration `AdaptiveVoteNotificationBatching` is additive (new columns, two indexes, two check constraints). It also
   moves windows left open by the old batching to `DUE` so they are sent at once. The API applies migrations at startup
   unless `DB_AUTO_MIGRATE=false`.
2. A rolling deploy is safe: old instances keep writing the old kind of rows, which the sweep sends.
3. Rollback: redeploy the old build. Rows classified in the new way (`UNDECIDED` events) are not understood by the old
   build, so wait for them to drain (seconds) before rolling back, or leave the migration applied.
4. Redis must allow `EVAL` with `TIME` inside scripts (standard Redis does). **This has not been verified on Upstash
   from this repository**; check it on a staging database before production (see below).
5. Polling costs Redis requests: one `EVAL` per second while a batch waits, one per `IdleRedisPollSeconds` when idle.
