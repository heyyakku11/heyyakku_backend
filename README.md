# Yakku Backend

ASP.NET Core API for Hey Yakku. Users prove email ownership with a 6-digit OTP. A user row is created only after verification succeeds. Until then, Redis holds a short-lived challenge — not a user.

Postgres stores users, profiles, sessions, guests, polls, votes, and system logs. Upstash Redis (REST) stores OTP challenges only.

## Stack

- .NET 10 / ASP.NET Core
- PostgreSQL (EF Core + Npgsql)
- Upstash Redis (OTP challenge store)
- JWT access tokens (15 minutes) + refresh sessions (30 days)
- FluentValidation, Swagger (Swashbuckle)

## Layout

| Project | Role |
|---|---|
| `src/Yakku.API` | HTTP controllers, middleware, Swagger, guest cookie |
| `src/Yakku.Application` | Auth, polls, votes, users, validators, DTOs |
| `src/Yakku.Domain` | Entities and enums |
| `src/Yakku.Infrastructure` | Postgres, Redis, email |

Auth internals (Redis key shape, challenge JSON) are in [`docs/INTRO.md`](docs/INTRO.md). OTP TTL in code is **5 minutes**.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (local or hosted)
- Upstash Redis REST credentials
- EF Core tools: `dotnet tool install --global dotnet-ef`

## Setup

```powershell
Copy-Item .env.example .env
```

Fill in `.env`. Never commit that file; `.env.example` is the template that stays in git.

| Variable | Required | Purpose |
|---|---|---|
| `DB_CONNECTION_STRING` | Yes | Postgres connection string |
| `UPSTASH_REDIS_REST_URL` | Yes | Redis REST base URL |
| `UPSTASH_REDIS_REST_TOKEN` | Yes | Redis REST token |
| `JWT_SECRET` | Yes | Access-token signing key (at least 32 characters) |
| `CORS_ORIGINS` | No | Comma-separated browser origins allowed to send credentials (`yakku_guest` cookie). Leave empty when the API and web app share the same origin. |
| `AWS_ENDPOINT_URL_S3` | Yes | Neon Object Storage S3 endpoint (path-style) |
| `AWS_REGION` | Yes | Storage region |
| `AWS_ACCESS_KEY_ID` | Yes | Storage access key |
| `AWS_SECRET_ACCESS_KEY` | Yes | Storage secret key |
| `POLL_MEDIA_IMAGE_BUCKET` | No | Image bucket (default `images`) |
| `POLL_MEDIA_VIDEO_BUCKET` | No | Video bucket (default `videos`) |
| `POLL_MEDIA_AUDIO_BUCKET` | No | Audio bucket (default `audio`) |
| `PORT` | No | Listen port. Used by the host (Docker / PaaS). Local launch settings use 5012 / 7086. |

Optional media policy knobs (defaults in parentheses): `POLL_MEDIA_IMAGE_MAX_BYTES` (10 MiB), `POLL_MEDIA_AUDIO_MAX_BYTES` (20 MiB), `POLL_MEDIA_VIDEO_MAX_BYTES` (100 MiB), `POLL_MEDIA_VIDEO_MAX_DURATION_SECONDS` (60), `POLL_MEDIA_AUDIO_MAX_DURATION_SECONDS` (120), pending/concurrent/storage quotas, upload/download URL TTLs, session TTL, cleanup interval. See [`.env.example`](.env.example).

Apply migrations:

```powershell
dotnet ef database update --project src/Yakku.Infrastructure --startup-project src/Yakku.API
```

## Run

```powershell
dotnet restore
dotnet run --project src/Yakku.API
```

Local URLs: `http://localhost:5012` and `https://localhost:7086`. In Development, Swagger UI is at `/swagger`.

HTTP samples: [`src/Yakku.API/Yakku.API.http`](src/Yakku.API/Yakku.API.http).

## Docker

```powershell
docker build -t yakku-api .
docker run --env-file .env -p 8080:8080 yakku-api
```

The image listens on `8080`. Pass the same env vars as `.env`.

## Auth

1. `POST /api/auth/request-otp` with `{ "email": "user@example.com" }`.
2. Emails are trimmed and lowercased. Missing user → registration challenge; existing user → login challenge.
3. OTP is 6 digits, hashed (SHA-256 of `email` + newline + otp), stored in Redis at `auth:otp:{email}` with a **5-minute** TTL.
4. Resend is blocked for 60 seconds (`OTP_RESEND_COOLDOWN`). After that the same key is overwritten.
5. `POST /api/auth/verify-otp` with `{ "email": "...", "otp": "483921" }`. Success on registration inserts `Users` + `UserProfiles`; success on login updates `LastLoginAt`. The Redis key is deleted either way. Response includes access and refresh tokens.
6. Five failed verifies → `OTP_ATTEMPTS_EXCEEDED` until a new OTP is requested.

If the code is never verified, the Redis key expires. No unverified user is left in Postgres.

In Development, OTP is logged (`LoggingEmailSender`), not emailed.

### Auth error codes

| Code | When |
|---|---|
| `OTP_RESEND_COOLDOWN` | New OTP requested within 60 seconds |
| `OTP_NOT_FOUND` | No challenge, or TTL expired |
| `OTP_INVALID` | Wrong code |
| `OTP_ATTEMPTS_EXCEEDED` | 5 failed verifies |
| `OTP_EXPIRED` | Key gone while updating attempts |
| `NOT_FOUND` | Login verify but user missing |
| `CONFLICT` | Could not allocate a unique display name |

## HTTP API

| Method | Path | Auth | Notes |
|---|---|---|---|
| `POST` | `/api/auth/request-otp` | — | Send / resend OTP |
| `POST` | `/api/auth/verify-otp` | — | Register or log in; returns JWT + refresh token |
| `POST` | `/api/auth/refresh` | — | Rotate refresh token |
| `POST` | `/api/auth/logout` | — | Revoke session |
| `GET` | `/api/users/me` | JWT | Current user profile |
| `POST` | `/api/v1/polls` | JWT | Create poll as **active** (`question` / `image` / `video` / `audio`; media types require completed upload) |
| `GET` | `/api/v1/polls/asked` | JWT | Polls I created, newest first (`cursor` query) |
| `GET` | `/api/v1/polls/answered` | JWT | Polls I voted on, most recently voted first (`cursor` query) |
| `GET` | `/api/v1/polls/saved` | JWT | Polls I saved, most recently saved first (`cursor` query) |
| `GET` | `/api/v1/polls/{id}` | JWT | My own poll with details and results (`404` if it is not mine). Others use the share link |
| `POST` | `/api/v1/polls/{id}/close` | JWT | Close my poll so it stops accepting votes (`409` if not active) |
| `DELETE` | `/api/v1/polls/{id}` | JWT | Delete my poll with its options, votes and media (`409` if its status does not allow it) |
| `PUT` | `/api/v1/polls/{id}/save` | JWT | Save a poll (idempotent) |
| `DELETE` | `/api/v1/polls/{id}/save` | JWT | Unsave a poll (idempotent) |
| `GET` | `/api/v1/shares/{shareCode}` | anonymous, JWT or guest | **Universal share link.** Resolves a poll *or* a made check (`contentType` `POLL`/`CHECK`) to one response shape: question, options, `voting` (`canVote`, `hasVoted`, `myVote`) and `stats`. Counts and percentages appear only when `stats.resultsVisible` (see [Voting](#voting-polls-and-checks)) |
| `POST` | `/api/v1/votes` | JWT **or** guest | **Unified vote** on a poll or a check: `{ shareCode, optionId \| customAnswer, reason? }`. `201` with the same shape as the share GET; `409` on a repeat vote, `403` for the owner of a check, `404` unknown/unreadable, `400` closed/expired/invalid option. Rate-limited |
| `POST` | `/api/v1/guests` | — | Create **or reuse** a guest: a valid `yakku_guest` cookie / `X-Guest-Token` returns the same guest with `guestToken: null`; a new `guestToken` (and cookie) is issued only when none is valid. Rate-limited per IP; `409` if the caller is already signed in |
| `POST` | `/api/v1/auth/verify` | — | Verify OTP; best-effort, atomic claim of the caller's valid guest, then the guest is deleted |
| `POST` | `/api/v1/devices/register` | JWT | Register / upsert the device the user is on and **link the current login session to it** (one record per installation + platform). Call it after every sign-in. `201` when created, `200` when updated. `409 DEVICE_CONFLICT` if the installation or push token belongs to another account |
| `PATCH` | `/api/v1/devices/me` | JWT | Partial update of the device linked to the current session: push-token rotation, metadata, notification permission. Omitted = unchanged, `""` = clear. `404` until the session has registered a device. Optional `X-Installation-Id` selects another of the user's own devices |
| `POST` | `/api/v1/media-upload-sessions` | JWT **or** guest | Start upload (presigned PUT); rate-limited |
| `GET` | `/api/v1/media-upload-sessions/{id}` | JWT **or** guest | Owner-only upload status |
| `GET` | `/api/v1/media-upload-sessions/{id}/access` | JWT **or** guest | Short-lived download URL (`uploaded` / `attached` only) |
| `POST` | `/api/v1/media-upload-sessions/{id}/complete` | JWT **or** guest | Verify object (size, magic bytes, duration) |
| `POST` | `/api/v1/media-upload-sessions/{id}/cancel` | JWT **or** guest | Cancel and purge unreferenced object |
| `GET` | `/api/system/health` | — | Postgres / Redis health |

Responses use a shared `ApiResponse` envelope. Guest auth is the HttpOnly `yakku_guest` cookie (7 days, `SameSite=Lax`) **or** header `X-Guest-Token: <guestToken>` (preferred for Flutter). JWT takes precedence when both are present.

## Voting (polls and checks)

A poll is created by a user; a check is an admin-managed template that a user *makes* into their own shareable check (`POST /api/v1/checks/make`). Both are shared with a **share code** and voted on through the same two endpoints, `GET /api/v1/shares/{shareCode}` and `POST /api/v1/votes`. The backend works out whether a code is a poll or a check (`share_codes` registry, unique across both).

- **Who votes:** a signed-in user (Bearer JWT) or a guest (`X-Guest-Token` / `yakku_guest` cookie). One vote per voter per poll or check. The owner of a check cannot vote on it (their own answer was recorded when they made it); the owner of a poll can.
- **Vote:** exactly one of `optionId` or `customAnswer` ("Other", max 300 characters), plus an optional `reason` (max 500). Votes are stored under a transaction that locks the poll/check row, so a vote cannot land after a close or deactivation.
- **Results rule (both types):** `stats.voteProgress` counts votes from people other than the owner. Below `stats.minVotesToReveal` (5) only the progress is returned. From 5 on, counts and percentages (`options[].voteCount` / `percentage`, `stats.totalVotes`, `customAnswerPercentage`, `whyCount`) are returned **only to the owner and to anyone who has voted**; everyone else keeps seeing the progress. A voter always sees which option they picked (`options[].isSelected`, `voting.myVote`). Percentages are of all votes including custom answers and the owner's own vote, rounded to two decimals.
- **Guest votes follow the guest:** when a guest signs in (`POST /api/v1/auth/verify`), their votes are moved to the user in the same transaction. A guest vote on something the user had already voted on is dropped, so the one-vote rule still holds.
- **Account deletion** removes the user's votes together with their polls and checks.

## Media uploads (Flutter)

Server selects the bucket from `mediaType` (`IMAGE` → images, `VIDEO` → videos, `AUDIO` → audio). Clients never send bucket names or object keys.

### Logged-in user

1. `POST /api/v1/media-upload-sessions` (Bearer JWT) with `{ "mediaType", "contentType", "fileSize", "selectionVersion", "fileName?" }`.
2. `PUT` the binary file to `upload.url` with the returned headers (do not Base64-encode).
3. `POST /api/v1/media-upload-sessions/{id}/complete` with `{ "selectionVersion" }`. Status becomes `uploaded`.
4. `POST /api/v1/polls` with `mediaUploadSessionId` — poll is created **active** immediately.

### Guest upload before login (claim on OTP)

1. `POST /api/v1/guests` — store `guestToken` from the body (returned only when a new token is issued).
2. Media APIs with header `X-Guest-Token: <guestToken>` (or the `yakku_guest` cookie on web): create → PUT → complete while the user writes the poll.
3. `POST /api/v1/auth/verify` with the guest token still present — backend transfers guest upload sessions to the user, deletes the guest row, and clears the cookie. Response includes JWT + `userId`.
4. `POST /api/v1/polls` with Bearer JWT and the same `mediaUploadSessionId`.

The claim is optional and never blocks sign-in:

- It runs in **one database transaction** (sessions move to the user and the guest is deleted together, or nothing changes) and locks the guest row, so concurrent claims of one guest have exactly one winner.
- Only a **valid, unexpired** guest is claimed. A missing, unknown or expired token still signs the user in; nothing is transferred.
- If the claim fails, the request still succeeds, the failure is audited, and the guest cookie is kept so the next sign-in can retry. The response contract is unchanged.

Allowed types: JPEG/PNG/WebP; MP4/WebM; MP3/M4A/WAV/OGG. Limits default to 10 / 100 / 20 MiB and 60s / 120s for video/audio. Abandoned and failed sessions are purged by a background worker.

Uploads use a single presigned PUT (no multipart). Actual size is enforced on complete, not by the signed URL. Do not log guest tokens or signed URLs.

### Guest expiry and cleanup

Guests expire 7 days after creation. A background job (`GuestCleanupWorker`) purges guests that have been expired for 7 days: it invalidates their upload sessions, deletes the stored objects, then removes the session rows and the guest. A guest whose objects could not be deleted yet is kept and retried on the next run, so the `Restrict` foreign key is never violated and no object is orphaned.

## Devices

An installation (`installationId` + platform) has **one owner**, taken from the validated JWT and never from the request body. A push token belongs to one installation (unique index on its SHA-256; raw tokens are never returned or logged). Device routes require sign-in; guests do not register devices.

**Sessions and push.** A user can be signed in on several devices at once (up to 5 live sessions). Each login session is linked to the device it runs on (`POST /api/v1/devices/register` sets `user_sessions.device_id`), and a notification to a user is sent only to devices that are active, have a push token and are linked to at least one session that is neither revoked nor expired. So a device stops receiving the moment its session ends (logout, eviction by a newer login, expiry), and a device that never registered after sign-in receives nothing. This is decided from the sessions at send time (`IDeviceRepository.GetPushTargetsByUserIdAsync`), not from a flag that has to be switched off.

| Event | What happens |
| --- | --- |
| Register again / token refresh | Same record is updated; no duplicates, even under concurrent requests |
| Token already on another installation of the **same owner** | Stale copy is cleared |
| Token or installation held by **someone else** | `409 DEVICE_CONFLICT`, no takeover |
| Notification permission `denied` | Token is cleared, push stops, record is kept |
| Guest signs in (register or login) | Guest installations move to the user inside the same transaction as the upload-session claim; expired / missing guest credential transfers nothing; a failure rolls everything back and never fails the login |
| Logout (single session) | Revokes that session only. That device receives no push once no live session is linked to it; the installation **stays linked to the account** (ownership never changes silently) and its token is cleared unless another live session still uses it. Other devices of the user are unaffected. The next login must register the device again |
| Logout all | Every session is revoked, so none of the user's devices receives push |
| Session evicted by a newer login (more than 5) | The evicted session's device stops receiving push, because it no longer has a live session |
| Next account on the same phone | May register the installation once the previous owner has no live session on it (i.e. after logout) |
| Guest purged after expiry | Its remaining installations are deleted with it |

## Audit logs and monitoring

| Table | Records |
| --- | --- |
| `authentication_audit_logs` | User / guest / admin sign-ins, OTP failures, session create / refresh / revoke, guest claims, and `SUSPICIOUS` events (exhausted OTP attempts, reuse of a revoked or rotated refresh token, failed admin password) |
| `poll_audit_logs` | Poll create / update / publish / close / delete, votes, rejected operations and permission failures. No foreign keys, so history survives poll and user deletion. Votes store a per-poll pseudonym and never the chosen option, answer or reason |
| `notification_audit_logs` | One row per push / email / SMS / WhatsApp attempt: provider, attempt number, retry flag, latency, sanitized error. `status` is **provider acceptance**; `delivery_status` stays `UNKNOWN` until a delivery report is recorded (FCM and Resend acceptance are not delivery) |
| `system_logs` | Unhandled exceptions, slow requests (over 1.5 s), 5xx responses, rate-limit rejections, and background-job runs / failures |

Credentials, tokens, OTPs, cookies and raw identifiers are never written: emails and phone numbers are masked or hashed, free text is redacted, and request paths are stored without IDs or query strings. Audit writes are best-effort and can never fail the request they describe. A retention job (`AuditRetentionWorker`) deletes rows past their retention window. Only the SMS and WhatsApp *schema* exists; no SMS/WhatsApp provider is wired up yet.

## Tests

```powershell
dotnet test
```

Unit tests mock storage. Schema integration tests use Testcontainers Postgres when Docker is available.
