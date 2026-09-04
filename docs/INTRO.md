# Yakku Backend Intro

Anonymous OTP-based API. Users prove email ownership with a 6-digit code. A **user row is created only after OTP succeeds**. Until then, Redis holds a short-lived challenge — not a user.

## Layout

| Project | Role |
|---|---|
| `Yakku.API` | HTTP controllers, middleware, Swagger |
| `Yakku.Application` | Auth/poll use cases, validators, DTOs |
| `Yakku.Domain` | `User`, `UserProfile`, polls, enums |
| `Yakku.Infrastructure` | Postgres (EF Core), Upstash Redis, email |

## Where data lives

| Data | Store | Key / table |
|---|---|---|
| Pending OTP challenge | Redis (Upstash REST) | `auth:otp:{email}` |
| Verified user | Postgres `Users` | unique `Email` |
| Display name, avatar | Postgres `UserProfiles` | unique `DisplayName` |
| Polls | Postgres `Polls` / `PollOptions` | — |

There is **no** Redis user key (`auth:user:{email}`, `user:{id}`, etc.). Redis is OTP-only.

Emails are trimmed and lowercased before lookup and Redis keys.

---

## Redis

### Key template

The only Redis key in this backend:

```
auth:otp:{email}
```

Example: `auth:otp:user@example.com`

### Value (`OtpChallenge` JSON)

```json
{
  "otpHash": "<sha256 hex of email + newline + otp>",
  "purpose": "Registration",
  "displayName": "yakku@12345",
  "attemptCount": 0,
  "createdAt": "2026-09-04T10:00:00Z"
}
```

- `otpHash` — SHA-256 of `{email}\n{otp}` (plaintext OTP is never stored).
- `purpose` — `Registration` if email is missing from DB, otherwise `Login`.
- `displayName` — reserved anonymous name for registration only (`yakku@{1000–99999}`). Login challenges set this to `null`.
- `attemptCount` — failed verify attempts (max 5).
- TTL — **10 minutes**. After that Redis deletes the key.

---

## Auth flow

### 1. `POST /api/auth/request-otp`

Body: `{ "email": "user@example.com" }`

1. Validate email.
2. Look up email in `Users`.
   - Missing → `Registration`
   - Present → `Login`
3. Load Redis `auth:otp:{email}` if it exists.

**Resend cooldown (60 seconds)**

- If a challenge exists and `now - createdAt < 60s` → `400 OTP_RESEND_COOLDOWN`. Existing OTP is unchanged.
- After 60s (key still alive up to 10 minutes) → **overwrite** the same key with a new OTP, `attemptCount = 0`, new `createdAt`, TTL reset to 10 minutes. The previous code is invalid.
- For registration, a previous `displayName` is reused so the reserved name stays stable across resends.

4. Generate a 6-digit OTP, hash it, `SET` Redis, send email.

Dev email is logged (`LoggingEmailSender`), not delivered.

### 2. `POST /api/auth/verify-otp`

Body: `{ "email": "user@example.com", "otp": "483921" }`

| Result | Redis | Database |
|---|---|---|
| No key / expired | — | unchanged |
| Wrong OTP | Same key, `attemptCount++`, original TTL kept | unchanged |
| 5 failed attempts | Key remains; verify blocked until a new OTP is requested (after cooldown) | unchanged |
| Success + **Registration** | Key **deleted** | Insert `Users` + `UserProfiles` |
| Success + **Login** | Key **deleted** | Update `LastLoginAt` |

If OTP is never verified, the Redis key expires in 10 minutes. No unverified user is left in Postgres.

---

## Users are not parked in Redis

Requesting OTP for a new email does **not** create a user.

Redis only holds the challenge (hash, purpose, reserved display name, attempts). The `User` is written in `CompleteRegistrationAsync` after a correct OTP.

If the user never verifies:

- Redis key expires.
- `Users` stays empty for that email.

---

## OTP rules

| Setting | Value |
|---|---|
| Length | 6 digits |
| TTL | 10 minutes |
| Resend cooldown | 60 seconds |
| Max verify attempts | 5 |
| Hash | SHA-256(`email` + `\n` + `otp`) |

---

## APIs

| Method | Path | Notes |
|---|---|---|
| `POST` | `/api/auth/request-otp` | Send / resend OTP |
| `POST` | `/api/auth/verify-otp` | Register or log in |
| `POST` | `/api/polls` | Create poll |
| `GET` | `/api/polls/{id}` | Get poll |

HTTP samples: `src/Yakku.API/Yakku.API.http`.

### Auth error codes

| Code | When |
|---|---|
| `OTP_RESEND_COOLDOWN` | New OTP requested within 60s |
| `OTP_NOT_FOUND` | No challenge, or TTL expired |
| `OTP_INVALID` | Wrong code |
| `OTP_ATTEMPTS_EXCEEDED` | 5 failed verifies |
| `OTP_EXPIRED` | Key gone while updating attempts |
| `NOT_FOUND` | Login verify but user missing |
| `CONFLICT` | Could not allocate a unique display name |

---

## Config

Copy `.env.example` to `.env`:

```
DB_CONNECTION_STRING=
UPSTASH_REDIS_REST_URL=
UPSTASH_REDIS_REST_TOKEN=
```

Postgres holds users and polls. Upstash Redis holds OTP challenges via REST (`SET` / `GET` / `DEL` / `TTL`).
