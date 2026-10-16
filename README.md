# Yakku Backend

ASP.NET Core API for Hey Yakku. Users prove email ownership with a 6-digit OTP. A user row is created only after verification succeeds. Until then, Redis holds a short-lived challenge — not a user.

Postgres stores users, profiles, sessions, guests, polls, votes, and system logs. Upstash Redis (REST) stores OTP challenges only.

## Stack

- .NET 10 / ASP.NET Core
- PostgreSQL (EF Core + Npgsql)
- Upstash Redis (OTP challenge store)
- JWT access tokens (15 minutes) + refresh sessions (7 days)
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
| `GET` | `/api/users/me/polls` | JWT | Current user’s polls (`cursor` query) |
| `POST` | `/api/v1/polls` | JWT | Create poll as **active** (`question` / `image` / `video` / `audio`; media types require completed upload) |
| `PATCH` | `/api/v1/polls/{id}` | JWT | Update draft only (legacy; new polls are active on create) |
| `POST` | `/api/v1/polls/{id}/publish` | JWT | Publish draft only (legacy; not needed for new creates) |
| `GET` | `/api/v1/polls/{id}` | JWT or anonymous | Get poll (signed media URL when ready) |
| `POST` | `/api/v1/polls/{id}/votes` | JWT | Cast vote |
| `POST` | `/api/v1/guests` | — | Create **or reuse** a guest: a valid `yakku_guest` cookie / `X-Guest-Token` returns the same guest with `guestToken: null`; a new `guestToken` (and cookie) is issued only when none is valid. Rate-limited per IP; `409` if the caller is already signed in |
| `POST` | `/api/v1/auth/verify` | — | Verify OTP; best-effort, atomic claim of the caller's valid guest, then the guest is deleted |
| `POST` | `/api/v1/media-upload-sessions` | JWT **or** guest | Start upload (presigned PUT); rate-limited |
| `GET` | `/api/v1/media-upload-sessions/{id}` | JWT **or** guest | Owner-only upload status |
| `GET` | `/api/v1/media-upload-sessions/{id}/access` | JWT **or** guest | Short-lived download URL (`uploaded` / `attached` only) |
| `POST` | `/api/v1/media-upload-sessions/{id}/complete` | JWT **or** guest | Verify object (size, magic bytes, duration) |
| `POST` | `/api/v1/media-upload-sessions/{id}/cancel` | JWT **or** guest | Cancel and purge unreferenced object |
| `GET` | `/api/system/health` | — | Postgres / Redis health |

Responses use a shared `ApiResponse` envelope. Guest auth is the HttpOnly `yakku_guest` cookie (7 days, `SameSite=Lax`) **or** header `X-Guest-Token: <guestToken>` (preferred for Flutter). JWT takes precedence when both are present.

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
