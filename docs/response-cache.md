# Redis GET response cache

Hot catalog and shared-read GET payloads use a **cache-aside** layer over Redis (`IResponseCache` / `RedisResponseCache`). Writes invalidate the relevant keys so lists do not keep showing a card the user just made.

If Redis is down or errors, the request **fails open**: the factory loads from Postgres and the API still returns successfully.

## Configuration

`appsettings.json` → `ResponseCache` (also bindable from env if needed):

| Setting | Default | Used for |
|---|---|---|
| `Enabled` | `true` | Master switch |
| `PlansSeconds` | `600` | Plans list |
| `CheckThemesSeconds` | `1800` | Check themes |
| `PersonalityTraitsSeconds` | `1800` | Personality traits |
| `FunCatalogSeconds` | `300` | Active fun-card definitions (no user flags) |
| `UserCatalogSeconds` | `60` | Per-user weekly checks / fun lists |
| `ShareSeconds` | `30` | Share page + share result |
| `ReasonsSeconds` | `30` | Poll/check option reasons + custom answers |

Keys are prefixed `yakku:resp:v1:`.

## Key matrix

### Shared catalogs

| Resource | Key | Invalidate on |
|---|---|---|
| Plans | `plans:all` | Plan update |
| Check themes | `check-themes:all` | Theme create |
| Personality traits | `personality:traits` | (TTL; admin trait writes when added) |
| Fun catalog | `fun:catalog` | Admin fun create / delete / theme create |

### User-scoped lists

| Resource | Key | Invalidate on |
|---|---|---|
| Weekly checks | `checks:week:{yyyy-MM-dd}:u:{userId}` or `:anon` | `Make` / delete made check; admin week publish (anon) |
| Fun list / types / by-type | `fun:list:u:…`, `fun:types:u:…`, `fun:type:{typeId}:u:…` | `Make`, save/unsave fun card |

### Share / reasons

| Resource | Key | Invalidate on |
|---|---|---|
| Share page | `share:{code}:v:{viewerId\|anon}` | Vote, close, reshare |
| Share result | `share:{code}:result` | Vote, close, reshare |
| Option reasons / custom answers | `reasons:…` | Vote (option / custom keys) |

## Hit / miss

1. **Hit** — deserialize JSON from Redis; no DB for that payload.
2. **Miss** — load DB → `SET` with TTL → return.
3. **After write** — `DEL` keys → next GET is a miss (fresh `used` / filtered list).
4. **TTL expiry** — automatic miss even if an invalidation was missed.
