# Ask Back

A private conversation between the owner of a poll or made check (the **asker**) and one signed-in voter, about that
voter's reason. One conversation per vote. Both sides see each other only by their Yakku anonymous name; user ids,
phone numbers and real names never appear in a response, an event or a push.

All endpoints need a Bearer JWT with a live session. Responses use the usual `ApiResponse` envelope
(`success`, `message`, `data`, `errors`, `meta`).

## Rules

- Only the owner of the poll/check can start a conversation, from a vote that has a reason, by a signed-in user (not
  a guest), who is not the owner. The server works out the participants from the vote; the client sends only `voteId`.
- Results are revealed only after `OperationalSettings.Results.MinVotesToReveal` other votes; Ask Back follows that.
- Only the two participants can read, send, join the real-time group or change anything. Anyone else gets `404`
  (the same as for an id that does not exist).
- The reason stays on the vote. It is shown as context and is never copied into a message. If the asker reports the
  reason, the conversation is closed (the asker can reopen it).
- **Turns.** The asker sends first, the voter replies once, then it is the asker's turn again. The number of questions
  the asker gets comes from the plan (below). When every allowed question has been answered the chat is done.
- **Report and end.** Blocking always files a report (`ask_back_reports`, reason `Not specified` if none was given) and
  ends the chat. See [Blocking](#blocking).

### Who can ask back: plans and unlocks

Plans are rows in `plans` with three perks. `null` means unlimited (for `duration_days`, never expires).

| Plan | Price (INR) | `duration_days` | `unlock_tickets` | `ask_back_message_limit` |
| --- | --- | --- | --- | --- |
| `free` (default) | ₹0 | null | 0 | 1 |
| `this_card` (catalog SKU; not activatable) | ₹49 | null | 1 | 1 |
| `yakku_lite` | ₹99 | 7 | 3 | 3 |
| `yakku_love` | ₹299 | 30 | null (unlimited) | null (unlimited) |

An admin can change them (`PATCH /api/v1/yakku/plans/{id}`, `-1` means unlimited for tickets and the limit). The
limit is read live, so a change also applies to conversations that already exist.

| Way to ask back | What it gives | Limit per reason | Expires |
| --- | --- | --- | --- |
| **Free ask back** | One per card, on the *top reason of any option*. | free plan's limit (1) | never |
| **Single-card unlock** (purchase) | Every reason on that card. | free plan's limit (1), or a higher active-plan limit | never |
| **`yakku_lite`** | Unlock up to 3 cards of your choice (a ticket each). | 3 | 7 days after activation |
| **`yakku_love`** | Unlimited cards, unlocked automatically on the first ask back. | unlimited | 30 days after activation |

- The *top reason* of an option is the newest visible reason from a registered user (guest reasons are skipped and a
  reason an admin blocked does not count). Each custom answer is its own option, so its reason is always its top.
  Using the free ask back on one reason uses it for the whole card (blocking or closing does not refund it).
  Everyone has this perk, including plan holders on cards they did not unlock. A per-card advisory lock makes the
  check and the insert one step.
- Buying the same plan again while it is active restarts the period from that moment and resets the tickets
  (tickets are the unlocks with source `PLAN` created at or after `PlanActivatedAt`, counted under a per-owner lock).
- **Expiry freezes plan chats.** When a plan ends, conversations on cards unlocked by it become read-only for both
  people (`isPaused: true`, `403 ASK_BACK_PLAN_EXPIRED` on send) until a plan is bought again. Single-card unlocks and
  free ask-back chats keep working. There is no background job: the effective plan is computed when it is read.
- No payment provider is integrated. The admin endpoints below are the hooks a payment flow will call.

## Blocking

Blocking a person is user-level and one way (blocker to blocked). It is never announced.

- The blocked person can no longer vote on the blocker's polls, checks or personality card, and cannot start an
  ask back with them. To them the cards read as closed (`BlockedReason` `CLOSED` for polls, `INACTIVE` for checks;
  the personality card is `NotCollectingVotes`).
- Starting a new conversation is refused in both directions.
- Only the blocker sees the blocked state (`canUnblock`). The app has no unblock button; the backend keeps
  `POST /{id}/unblock`, which also removes the user block.

### Status policy

| Status | Meaning | Who can change it |
| --- | --- | --- |
| `active` | Both can send (subject to turns). | |
| `closed` | Read-only. | Either participant closes. Only the **asker** reopens. |
| `blocked` | Read-only. Separate from closed. | Either participant blocks. Only **whoever blocked** lifts it. |

The close and reopen endpoints stay in the API, but the app only shows reopen for a closed conversation that
`canReopen`.

### Account lifecycle

- Pending deletion (30-day window): the account cannot be asked back or send. Logging in again cancels the deletion
  and everything works again.
- Permanent deletion: the conversation and messages stay for the other person. The deleted side is shown as
  `Deleted user` (`isDeleted: true`), and the conversation is read-only (`canSend: false`). The foreign keys are
  `ON DELETE SET NULL`, so `AccountDeletionRepository` needed no change.
- If the voter's vote is deleted (their account purged), `context.selectedOption` and `context.reason` are `null`.

## REST

Base: `/api/v1/ask-back/conversations`. Rate limits per user per minute: writes 60, reads 240.

| Method and path | Purpose |
| --- | --- |
| `POST /` | Start (or fetch) the conversation for a vote. |
| `GET /` | List my conversations. |
| `GET /{id}` | One conversation. |
| `GET /{id}/messages` | Message history. |
| `POST /{id}/messages` | Send a message. |
| `PATCH /{id}/read` | Move my read position. |
| `POST /{id}/close`, `POST /{id}/reopen` | Close / reopen. |
| `POST /{id}/block`, `POST /{id}/unblock` | Block (always reports) / lift. |

Unlocking and access, base `/api/v1/ask-back`:

| Method and path | Purpose |
| --- | --- |
| `GET /access?voteId=` | Whether the card of that vote is unlocked, how, tickets left, plan expiry and the limit. |
| `POST /unlocks` `{ "voteId" }` | Unlock the card with a plan ticket. Unlocking an unlocked card costs nothing. |

Admin (`Admin` policy), base `/api/v1/yakku`:

| Method and path | Purpose |
| --- | --- |
| `POST /users/{userId}/plan` `{ "planCode" }` | Activate a plan from now (restarts the period and tickets). |
| `POST /users/{userId}/card-unlocks` `{ "contentType": "poll"\|"check", "contentId" }` | Grant a single-card unlock (source `PURCHASE`). |
| `PATCH /plans/{planId}` | Edit name, price, `durationDays`, `unlockTickets`, `askBackMessageLimit`. |

### POST /

```json
{ "voteId": "uuid" }
```

`201` when created, `200` when it already existed. `400` own vote or no reason, `403` guest voter or an account that
cannot take part or a blocked pair (`ASK_BACK_NOT_ALLOWED`), `403 ASK_BACK_LOCKED` when the card is not unlocked and
this is not the card's free top reason, `403 ASK_BACK_PLAN_EXPIRED` on a card whose plan unlock is paused, `404`
unknown vote or not the owner. Concurrent calls are safe: the unique index on `vote_id` decides and the loser returns
the winner's conversation.

### Conversation

```json
{
  "id": "uuid",
  "status": "active",              // active | closed | blocked
  "myRole": "asker",               // asker | voter
  "other": { "anonymousName": "Quiet Fox", "isDeleted": false },
  "context": {
    "contentType": "poll",         // poll | check
    "question": "Which is better?",
    "selectedOption": "Option A",  // or the custom answer
    "reason": "Because it is clearer"
  },
  "createdAt": "2026-10-30T09:00:00Z",
  "closedAt": null,
  "unreadCount": 0,
  "turn": "me",                    // me | them | done
  "messagesLeft": 1,               // my questions (asker) or replies (voter) left; null when unlimited
  "isPaused": false,               // read-only because the asker's plan expired
  "canSend": true,                 // active, not paused and my turn
  "canReopen": false,
  "canUnblock": false
}
```

### GET /?cursor=&limit=

Most recently active first. `limit` default 20, max 50. `meta.nextCursor` / `meta.hasMore` page forward.

```json
{
  "id": "uuid", "status": "active", "myRole": "asker",
  "other": { "anonymousName": "Quiet Fox", "isDeleted": false },
  "lastMessage": { "preview": "first 80 chars…", "isMine": false, "createdAt": "…" },
  "lastMessageAt": "…", "lastActivityAt": "…", "unreadCount": 2, "createdAt": "…"
}
```

### GET /{id}/messages?cursor=&direction=&limit=

Items are always oldest first within a page. `limit` default 30, max 50.

- No cursor: the newest page.
- `direction=older` (default) + `cursor`: the page before that position. Scroll up with `meta.nextCursor`.
- `direction=newer` + `cursor` (required): the messages after that position. Use it after a reconnect with the
  `cursor` of the newest message you have; follow `meta.nextCursor` while `meta.hasMore`.

The cursor is an opaque `(created_at, id)` position, so paging has no gaps or duplicates even for equal timestamps.

```json
{
  "id": "uuid", "conversationId": "uuid", "clientMessageId": "uuid",
  "content": "Why did you pick that?", "senderName": "Quiet Fox",
  "isMine": false, "createdAt": "…", "cursor": "opaque"
}
```

`senderUserId` is never returned.

### POST /{id}/messages

```json
{ "clientMessageId": "uuid", "content": "1 to 2000 characters" }
```

`201` with the message. The id and time are server-generated; content is trimmed. Sending the same
`clientMessageId` again returns the stored message (no second row, no second event). `400` empty/too long, `404` not
a participant. `409` codes:

| Code | When |
| --- | --- |
| `ASK_BACK_NOT_ACTIVE` | Closed, blocked or the other account is gone. |
| `ASK_BACK_NOT_YOUR_TURN` | The other person has to write first (or a concurrent send won). |
| `ASK_BACK_LIMIT_REACHED` | Every allowed question is answered. |

`403 ASK_BACK_PLAN_EXPIRED` when the plan that unlocked the card has expired. The turn counters on the conversation
are concurrency tokens, so two simultaneous sends cannot both succeed.

### PATCH /{id}/read

```json
{ "lastReadMessageId": "uuid" }
```

Returns `{ "lastReadMessageId", "advanced", "unreadCount" }`. A position that is not newer than the current one is
ignored (`advanced: false`); a message of another conversation is `404`.

### Close, reopen, block, unblock

All return the updated conversation. `block` takes an optional `{ "reason": "up to 500 chars" }` and always files a
report. Close, reopen, block and unblock are idempotent. `403` for a reopen by the voter or an unblock by the person
who did not block; `409` for reopening or closing a blocked conversation.

### Reasons lists

`ReasonResponse` and `CustomAnswerResponse` carry `askBackLocked`: true when asking back on that reason needs an
unlock (the card is not unlocked, the reason is not the one free top reason, and no conversation exists for it). The
app opens the unlock sheet instead of a chat.

On the free plan only each option's top reason is readable. Every other reason of the option comes with
`isHidden: true` and an empty `text` (the real text is never sent), and the list has `hiddenCount`. A reason is
revealed for the whole card when the card was bought, or while a paid plan is active. Hidden reasons still carry
`canAskBack`, `askBackLocked` and `reportStatus`.

Custom answers ("Something else") follow the same rule. On the free plan one answer stays open: the newest
registered user's answer that has a reason, else the newest registered user's, else the newest. Every other one
comes with `isHidden: true`, an empty `answer` and a null `reason` (never sent), and the list has `hiddenCount`.

`GET /api/v1/ask-back/card-access?contentType=&contentId=` returns the owner's access for a card (same body as
`/access`, with `reasonsRevealed`). The results screen uses it to show the "Unlock now" bar on the free plan only.

## SignalR hub: `/hubs/ask-back`

Authenticate with the same access token. Mobile/WebSocket clients pass it as `?access_token=<jwt>` (only accepted
on `/hubs/*`). A connection of a logged-out session is refused, and every hub call re-checks the session.

**Messages are sent over REST, never through the hub.** The hub only delivers. The database is the source of truth;
after a reconnect, fetch what you missed with `messages?direction=newer&cursor=<last cursor>`.

Client to server:

| Method | Notes |
| --- | --- |
| `JoinConversation(conversationId)` | Fails with `HubException("Conversation not found.")` unless you are a participant. The server builds the group name `conversation:{id}`. |
| `LeaveConversation(conversationId)` | |
| `TypingStarted(conversationId)` / `TypingStopped(conversationId)` | Only after joining. |

Server to client (target is the user, so every device of that user gets it, joined or not, unless noted):

| Event | Payload |
| --- | --- |
| `ReceiveMessage` | The message object above, with `isMine` set for the receiving side (the sender's other devices get `true`). |
| `MessageRead` | `{ conversationId, lastReadMessageId, readAt }`: the other participant read up to there. |
| `ConversationClosed` | `{ conversationId, status }` where status is `closed` or `blocked`. |
| `ConversationReopened` | `{ conversationId, status: "active" }` (closed reopened, or a block lifted). |
| `UserTyping`, `UserStoppedTyping` | `{ conversationId }`, only to the other joined connection(s). |

Delivery is best effort and happens after the message is committed. The in-memory presence used to skip pushes and
the SignalR user targeting are per server instance; see Limitations.

## Push

A message writes an `ask_back_outbox` row in the same transaction. `AskBackPushWorker` claims due rows with a lease
(like the vote notification outbox), waits `PushDelay` (3 s), and sends through the existing
`IPushNotificationService` (same device eligibility rules). It is **skipped** when the recipient has a connection
joined to the conversation. The payload carries no text or name:

```json
{ "title": "Yakku", "body": "You have a new message",
  "data": { "type": "normal", "eventType": "AskBackMessage",
            "conversationId": "uuid", "deepLink": "yakku://ask-back/<conversationId>" } }
```

Retries use the shared backoff (4 attempts), then `FAILED`.

## Database

Migration `AddAskBack` adds `ask_back_conversations`, `ask_back_participants`, `ask_back_messages`,
`ask_back_outbox` and `ask_back_reports`. Constraints that matter: unique `vote_id`; unique
`(conversation_id, user_id)`; unique `(conversation_id, sender_user_id, client_message_id)`; message length check
1..2000; status check; distinct participants check; blocked-by consistency check.

Migration `AskBackPlansUnlocksAndTurns`:

- `plans`: `duration_days`, `unlock_tickets`, `ask_back_message_limit` (check constraints), seeded as in the table above.
- `users`: `PlanActivatedAt`, `PlanExpiresAt`.
- `card_unlocks`: one row per owner and card (unique `IX_card_unlocks_owner_card`), `source` `PLAN` or `PURCHASE`.
  Cards that already had a conversation are backfilled as `PURCHASE` unlocks for the asker, so they keep working.
- `ask_back_conversations`: `asker_message_count` and `voter_message_count` (concurrency tokens), backfilled from the
  existing messages.
- `user_blocks`: one row per blocker and blocked person (unique pair).
- Drops `ask_back_participants.is_muted` (mute was removed).

## Limitations and open points

- SignalR delivery and presence are in-memory per instance. With more than one API instance, add a backplane
  (Redis) before scaling out; the push skip would otherwise miss a user joined on another instance, costing one
  redundant push. REST remains correct either way.
- Rows in `ask_back_outbox` are not purged yet (the vote notification outbox has a retention worker).
- Ending a conversation because a reported reason removed it does not push a real-time event; it shows on the next
  fetch.
- Message edit and delete columns exist (`edited_at`, `deleted_at`) but there are no endpoints for them.
- There is no payment provider yet; plans and single-card unlocks are granted through the admin endpoints.
- Blocks made before this change were conversation-level only and were not turned into user blocks.
