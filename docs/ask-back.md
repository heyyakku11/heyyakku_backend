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

### Status policy (product decisions, confirmed)

| Status | Meaning | Who can change it |
| --- | --- | --- |
| `active` | Both can send. | |
| `closed` | Read-only. | Either participant closes. Only the **asker** reopens. |
| `blocked` | Read-only. Separate from closed. | Either participant blocks. Only **whoever blocked** lifts it. |

Blocking applies to the conversation only; it is not a global user block. An optional `reason` on block is also
recorded as a report (`ask_back_reports`) for moderation.

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
| `PUT /{id}/mute`, `DELETE /{id}/mute` | Mute / unmute pushes. |
| `POST /{id}/close`, `POST /{id}/reopen` | Close / reopen. |
| `POST /{id}/block`, `POST /{id}/unblock` | Block / lift. |

### POST /

```json
{ "voteId": "uuid" }
```

`201` when created, `200` when it already existed. `400` own vote or no reason, `403` guest voter or an account that
cannot take part (`ASK_BACK_NOT_ALLOWED`), `404` unknown vote or not the owner. Concurrent calls are safe: the unique
index on `vote_id` decides and the loser returns the winner's conversation.

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
  "isMuted": false,
  "unreadCount": 0,
  "canSend": true,
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
  "lastMessageAt": "…", "lastActivityAt": "…", "unreadCount": 2, "isMuted": false, "createdAt": "…"
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
`clientMessageId` again returns the stored message (no second row, no second event). `400` empty/too long,
`409 ASK_BACK_NOT_ACTIVE` when closed, blocked or the other account is gone, `404` not a participant.

### PATCH /{id}/read

```json
{ "lastReadMessageId": "uuid" }
```

Returns `{ "lastReadMessageId", "advanced", "unreadCount" }`. A position that is not newer than the current one is
ignored (`advanced: false`); a message of another conversation is `404`.

### Mute, close, reopen, block, unblock

All return the updated conversation. `block` takes an optional `{ "reason": "up to 500 chars" }`. Close, reopen,
block, unblock and mute are idempotent. `403` for a reopen by the voter or an unblock by the person who did not
block; `409` for reopening or closing a blocked conversation.

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
`IPushNotificationService` (same device eligibility rules). It is **skipped** when the recipient muted the
conversation or has a connection joined to it. The payload carries no text or name:

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

## Limitations and open points

- SignalR delivery and presence are in-memory per instance. With more than one API instance, add a backplane
  (Redis) before scaling out; the push skip would otherwise miss a user joined on another instance, costing one
  redundant push. REST remains correct either way.
- Rows in `ask_back_outbox` are not purged yet (the vote notification outbox has a retention worker).
- Ending a conversation because a reported reason removed it does not push a real-time event; it shows on the next
  fetch.
- Message edit and delete columns exist (`edited_at`, `deleted_at`) but there are no endpoints for them.
- Reporting is only available together with blocking.
