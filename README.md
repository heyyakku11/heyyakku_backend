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
| `tests/Yakku.Application.Tests` | Application-layer tests |

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
| `PORT` | No | Listen port. Used by the host (Docker / PaaS). Local launch settings use 5012 / 7086. |

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

## Tests

```powershell
dotnet test
```

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
| `POST` | `/api/polls` | JWT | Create poll (2–10 unique options) |
| `GET` | `/api/polls/{id}` | Guest cookie | Get poll; sets `yakku_guest` if missing |
| `POST` | `/api/polls/{id}/votes` | Guest cookie | Cast vote; one vote per guest per poll |
| `GET` | `/api/system/health` | — | Postgres / Redis health |
| `POST` | `/api/system/otp/decrypt` | — | Helper to recover a pending OTP from Redis |

Responses use a shared `ApiResponse` envelope. Guest identity is an HttpOnly `yakku_guest` cookie (1 year, `SameSite=Lax`).
