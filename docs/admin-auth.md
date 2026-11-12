# Admin authentication (HttpOnly cookie sessions)

Admins no longer receive JWTs. Signing in sets an **opaque session token in an HttpOnly cookie**; the session itself
lives in PostgreSQL (`admin_sessions`). Customer and mobile authentication (JWT + refresh tokens) is unchanged.

## Flow

1. `POST /api/v1/admin/auth/login` (email, password). On success the response **sets the cookie** and the body is only
   `{ "csrfToken": "..." }`. No token, email or role is returned.
2. `GET /api/v1/admin/auth/session` returns `{ email, role, csrfToken }`. The frontend calls it on load to restore its
   state; the server cookie is the source of truth, browser storage holds nothing.
3. Every admin request carries the cookie automatically. **State-changing requests** (POST/PUT/PATCH/DELETE) must also
   send `X-CSRF-Token: <csrfToken>` and come from a trusted `Origin`.
4. `POST /api/v1/admin/auth/logout` revokes this session on the server and clears the cookie.
   `POST /api/v1/admin/auth/logout-all` revokes every session of the admin.
5. `POST /api/v1/admin/auth/refresh` is **removed**: there is no client-side refresh. Sessions renew on the server.

| Status | Meaning |
| --- | --- |
| `401` | No cookie, or the session is unknown, expired, revoked, or the admin is no longer active |
| `403` | Untrusted `Origin`, or missing/invalid CSRF token |
| `429` | Too many failed logins (generic message) |

## Sessions

* The cookie value is 32 random bytes (256 bits, base64url). **Only its SHA-256 is stored**; the token is never logged,
  audited or returned in a body.
* A login always creates a brand-new session and token, so a client-supplied value is never reused (no fixation).
  Several devices can be signed in at once.
* **Idle timeout** (default 30 min, extended by activity) and **absolute timeout** (default 12 h, never extended).
* **Rotation:** once the token is `ADMIN_SESSION_ROTATION_INTERVAL` old, the next request swaps it for a new one (new
  `Set-Cookie`). The previous token keeps working for 30 s so in-flight and concurrent requests never fail; the swap is
  a single conditional `UPDATE`, so exactly one of many concurrent requests wins.
* Every request re-checks the session **and the admin account** in the database. Disabling an admin ends all their
  sessions on the next request. Changing or resetting the password revokes all sessions.
* Ended sessions stay as rows (with `revoked_at` / `revocation_reason`) for the audit window, then the existing session
  cleanup job deletes them.

## Cookie

`HttpOnly`, `Secure` (required outside development), `SameSite=Strict` by default, `Path=/`, no `Domain` (host-only),
explicit `Max-Age`/`Expires` (remaining absolute lifetime). Production name is `__Host-yakku_admin` (the browser then
also enforces Secure + Path=/ + no Domain). Logout clears it with the same attributes.

## CSRF

Two independent layers, both required for state-changing admin requests (login and the other unauthenticated admin
auth routes get the Origin check only, since there is no session yet):

1. **Origin check:** `Origin` (or `Referer` as fallback) must be one of `ADMIN_WEB_BASE_URL`, `ADMIN_TRUSTED_ORIGINS`
   or `CORS_ORIGINS`. A missing Origin *and* Referer is rejected.
2. **Session-bound token:** `X-CSRF-Token` = `HMAC-SHA256(key, sessionId)`, compared in constant time. The key is
   derived (HKDF) from `JWT_SECRET`. The token is not a credential, so the browser may hold it in memory.

`SameSite` is a third layer, not the defence. `GET`/`HEAD`/`OPTIONS` never change state and are not checked.

## Login protection

* Generic `401` for an unknown email and a wrong password; an unknown email still costs a hash check (no timing leak).
* Throttled on three axes, all with **growing but capped, self-expiring blocks** (15 s doubling to 15 min), never a
  permanent lock: the *(email, IP)* pair (5 failures), the *account* from any IP (20), and the *IP* across all emails
  (30, credential stuffing). A blocked client gets `429` even with the right password; an unknown email is blocked the
  same way, so blocks reveal nothing. The owner signing in from another address is not locked out by an attacker.
* `ADMIN_LOGIN_RATE_LIMIT` (default 10/min) additionally limits admin auth requests per client.

## Audit events (`authentication_audit_logs`)

`login.succeeded`, `login.failed`, `admin.login_throttled`, `session.created`, `admin.session_rotated`,
`admin.session_expired`, `admin.session_rejected` (unknown/malformed/revoked token; revoked = `SUSPICIOUS`),
`session.revoked`, `session.revoked_all`, `admin.csrf_rejected`, `admin.origin_rejected`, and the password events.
They carry admin id, session id, IP, user agent and a reason code. Never passwords, tokens, hashes or cookies.

## Environment variables

| Variable | Default | Notes |
| --- | --- | --- |
| `ADMIN_WEB_BASE_URL` | required | Also the first trusted origin. |
| `ADMIN_TRUSTED_ORIGINS` | — | Extra trusted origins, comma separated. |
| `ADMIN_COOKIE_NAME` | `__Host-yakku_admin` / `yakku_admin` (dev) | |
| `ADMIN_COOKIE_SAME_SITE` | `Strict` | `Strict`, `Lax`, `None` (needs Secure). |
| `ADMIN_COOKIE_SECURE` | `true` outside development | Startup fails if false outside development. |
| `ADMIN_CSRF_ENABLED` | `true` | Startup fails if false outside development. |
| `ADMIN_SESSION_IDLE_TIMEOUT` | `00:30:00` | `hh:mm:ss`, at least 1 minute. |
| `ADMIN_SESSION_ABSOLUTE_TIMEOUT` | `12:00:00` | At least the idle timeout, at most 30 days. |
| `ADMIN_SESSION_ROTATION_INTERVAL` | `00:15:00` | At least 1 minute. |
| `ADMIN_LOGIN_RATE_LIMIT` | `10` | Requests per minute, 1-1000. |
| `TRUSTED_PROXY_HOPS` | `1` | Proxies that append to `X-Forwarded-For` (see below). |

Anything outside the `Development` environment is treated as production. Invalid or insecure values stop the app from
starting (`AdminSecuritySettings.Load`).

## Deployment

The admin app (Vercel) and API (Render) are different sites, so a `SameSite=Strict` cookie would never be sent. The
admin app therefore calls `/api/...` **on its own origin** and `vercel.json` rewrites that to the backend, which makes
the cookie first-party. Requirements:

1. `heyyakku_admin/api/proxy.ts` (a Vercel edge function, wired by the rewrite in `vercel.json`) forwards `/api/*` to the address in the
   `VITE_API_BASE_URL` environment variable. Nothing is hardcoded: set that variable in the Vercel project (Production and
   Preview) to the deployed API address, for example the Render URL, and redeploy.
2. The admin app itself never reads `VITE_API_BASE_URL`; it always calls `/api` on its own origin. The variable is only
   the forwarding address used by the function (and by the Vite dev server locally).
3. `ADMIN_WEB_BASE_URL` on the API = the exact Vercel origin (it is the trusted `Origin`).
4. `TRUSTED_PROXY_HOPS=2` on Render (Vercel then Render both append to `X-Forwarded-For`), otherwise every admin shares
   one apparent IP (Vercel's) and the IP throttle and audit IPs are wrong. Verify in the audit log after deploy.
5. First deploy after the migration signs every admin out once (old refresh-token sessions are deleted by the
   `AdminCookieSessions` migration; admin accounts are untouched).
6. Local development: put the API address in the admin app's `.env` as `VITE_API_BASE_URL`; the Vite dev server proxies `/api` to it (`vite.config.ts`), so the
   cookie (`yakku_admin`, not Secure over http) is first-party here too. `CORS_ORIGINS` should include
   `http://localhost:5173`.

If you later put both apps on one registrable domain (`admin.example.com` + `api.example.com`) the rewrite is no longer
required, but then use `SameSite=Lax` and set `ADMIN_TRUSTED_ORIGINS`; keep `Domain` unset.

## Tests

* `AdminSessionServiceTests`: token hashing, expiry (idle/absolute), revocation, suspension, rotation + grace, concurrent
  rotation, logout / logout-all, audit hygiene, CSRF token binding.
* `AdminHttpSecurityTests`: a real Kestrel host running the production handler, cookie service and CSRF/Origin
  middleware: 401/403 behaviour, cookie attributes, logout clearing, CSRF missing/invalid/other-session, untrusted and
  missing Origin, Referer fallback, login CSRF, rotation over HTTP, legacy admin JWT rejection, user token on admin
  route, caching and security headers, and startup validation.
* `AdminAuthFlowTests`: login throttling (pair, account, IP, unknown email, lifts by itself), generic errors, password
  change/reset revoke sessions.
* `AdminSessionRepositoryIntegrationTests`: the real SQL atomics (needs Docker; skipped otherwise).
