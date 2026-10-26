# User streaks

A streak counts consecutive days on which a registered user did something that counts. It belongs to the user
account only; devices, sessions, logins and logouts never touch it.

## Rules

- **Qualifying activities** are defined in one place, `StreakRules.Qualifies` (`Application/Streaks/StreakActivity.cs`):
  creating a poll (`PollService.CreateAsync`) and an accepted vote by a signed-in user (`VotingService`).
  Opening the app, viewing, logging in, receiving a notification, guest votes, admin actions and making a check do not
  count. Votes moved from a guest to an account at sign-in are not credited retroactively.
- A day counts **once per user**, however many qualifying actions happen on it.
- First activity: streak 1. Next calendar day: +1. Same day: unchanged. After one or more whole missed days: back to 1.
- The **longest streak** is the maximum ever reached and is never reduced. Daily history is never deleted when a streak
  breaks.
- The **current streak shown** is zero once a whole day has been missed (computed when it is read, no background job).
  It is still shown while the last activity was today or yesterday, so the user can still extend it today.

## Day policy: UTC calendar days

A streak day is the UTC date of the business record's timestamp (`poll.CreatedAt`, the vote's `CreatedAt`), computed on
the server. The product has no timezone setting and `Device.Timezone` is unvalidated client text, so it is deliberately
ignored. Consequences:

- Clients cannot supply a date, and a device clock has no effect.
- Travelling or changing a timezone cannot create, move or remove a streak day. Daylight saving has no effect.
- The day rolls over at 00:00 UTC, which is not local midnight for most users. If a per-user timezone is added later,
  only `StreakCalendar.ToActivityDate` and the read-time "today" need to change, and existing rows would need a decision
  on how to interpret old days.

## Data

- `user_activity_days (user_id, activity_date date, first_activity_at, created_at)`, primary key
  `(user_id, activity_date)`. The authoritative history, one row per user per day.
- `user_streaks (user_id, current_streak, longest_streak, last_activity_date, updated_at)`. A summary that exists only
  after the first activity. Constraints: `current_streak >= 1`, `longest_streak >= current_streak`.
- Both cascade-delete with the user. Nothing is backfilled: streaks start from release, and the migration only adds tables.

## Consistency

`StreakService.RecordAsync` runs **inside the transaction of the business operation** (poll creation, and the vote
transaction through `AddVoteAsync`'s `afterAdded` callback). A rejected, duplicate or rolled-back operation therefore
earns nothing, and a committed one cannot lose its credit. A failure in the streak write rolls the operation back.

1. `INSERT ... ON CONFLICT (user_id, activity_date) DO NOTHING` decides whether the day is new (retries and concurrent
   requests are idempotent, and the common repeat costs one statement).
2. For a new day, the summary row is locked (`SELECT ... FOR UPDATE`) so one user's writers apply one at a time, across
   application instances.
3. `StreakMath.Next` applies the arithmetic. A day *earlier* than the last counted day (late or out-of-order) never edits
   the summary directly; the summary is rebuilt from the day history in one SQL statement (gaps and islands).

`RebuildAsync(userId)` can be run at any time to repair a summary from history.

## API

- `GET api/v1/users/streak`: `currentStreak`, `longestStreak`, `lastActivityDate`, `isActiveToday`, `today`.
- `GET api/v1/users/streak/history?from=&to=`: active dates, at most 366 days (default: the last 30).

Both read the signed-in user's id from the token and accept no user id.

## Operations

- Adding a qualifying activity: add an enum value and include it in `StreakRules.Qualifies`, then call
  `IStreakService.RecordAsync` inside the operation's transaction.
- The database stores summaries computed by the application; an invalid value is rejected by check constraints.
