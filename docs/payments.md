# Payments (Razorpay)

One-time Razorpay Orders/Payments for `yakku_lite`, `yakku_love`, and `this_card`. Recurring Razorpay Subscriptions are out of scope.

## Locked decisions

- Reuse `card_unlocks` for Lite tickets, permanent ownership, and Free read-only history (no `UserPlanCardUsage` / `UserCardAccessHistory`).
- Add `user_plan_entitlements` for paid entitlement history; keep `users.PlanId` / `PlanActivatedAt` / `PlanExpiresAt` as the live projection.
- Routes under `/api/v1/payments` and `/api/v1/webhooks/razorpay`.
- Reuse Ask Back access APIs for feature authorization.
- `this_card` is the permanent card SKU on the same order flow.
- Admin activate/grant remain unrestricted overrides; the paid path enforces purchase rules.
- Refunds in Phase 6 via admin API; refunds never auto-revoke access.

## Phase status

| Phase | Status |
|---|---|
| 0 Audit | Done |
| 1 Database + config + payment logs | Done |
| 2 Razorpay gateway + order creation | Done |
| 3 Verify + attempts | Done |
| 4 Webhooks | Done |
| 5 Entitlement fulfillment wiring | Done |
| 6 Reconciliation / history / ops | Done |

## Schema (Phase 1)

| Table | Purpose |
|---|---|
| `payment_orders` | Internal order, amount from catalog, plan snapshot JSONB, idempotency |
| `payment_attempts` | Gateway attempts; unique Razorpay payment id |
| `user_plan_entitlements` | Paid periods (`ACTIVE`/`EXPIRED`/`SUPERSEDED`/`REVOKED`); one `ACTIVE` per user |
| `webhook_events` | Durable inbox; unique Razorpay event id |
| `payment_logs` | Long-lived payment audit (not subject to system-log retention) |
| `payment_refunds` | Admin-initiated Razorpay refunds (no auto access revoke) |
| `card_unlocks` | Added nullable `user_plan_entitlement_id`, `payment_order_id` |

## Configuration

Flat env keys bridge into nested sections (same pattern as MSG91):

- `RAZORPAY_KEY_ID` / `RAZORPAY_KEY_SECRET` / `RAZORPAY_WEBHOOK_SECRET` / `RAZORPAY_BASE_URL`

Non-secret `Payment:*` defaults (`Provider`, `Currency`, `CaptureMode`, `OrderExpiryMinutes`) live in `appsettings.json` (not `.env`).

`Razorpay:Enabled` is `false` in Development and `true` in Staging/Production. When enabled, missing secrets fail startup. Only the Key ID is ever returned to Checkout clients.

## Phase 2 API

`POST /api/v1/payments/orders` (JWT + live session)

```json
{
  "planId": "<yakku_lite | yakku_love | this_card guid>",
  "contentType": "poll|check",
  "contentId": "<guid>",
  "idempotencyKey": "client-unique-key"
}
```

`contentType` / `contentId` are required only for `this_card`. Response includes `orderId`, `razorpayOrderId`, `razorpayKeyId`, `amountInPaise`, `currency`.

### Verify

`POST /api/v1/payments/verify`

```json
{
  "razorpayOrderId": "order_xxx",
  "razorpayPaymentId": "pay_xxx",
  "razorpaySignature": "hex-hmac"
}
```

Backend checks HMAC (`orderId|paymentId` with key secret), loads the payment from Razorpay, requires `captured`, matches amount/currency/order, then idempotently fulfills (plan entitlement or permanent card unlock). Duplicate verify callbacks return `alreadyFulfilled: true`.

## Phase 4 Webhooks

`POST /api/v1/webhooks/razorpay` (anonymous; rate-limited)

1. Read the **raw** body bytes (signature must not run on re-serialized JSON).
2. Verify `X-Razorpay-Signature` with `RAZORPAY_WEBHOOK_SECRET` (HMAC-SHA256 hex).
3. Persist to `webhook_events` keyed by Razorpay event id (`X-Razorpay-Event-Id` or payload `id`, else SHA-256 of body).
4. Return `200` immediately; a background worker claims inbox rows and processes them.

Handled event types:

| Event | Behavior |
|---|---|
| `payment.captured` / `order.paid` | Same captured-only fulfillment path as verify |
| `payment.authorized` | Audit log only; no access grant |
| `payment.failed` | Record failed attempt; never downgrade an already-captured payment/order |
| other | Ack and ignore |

Duplicates of the same Razorpay event id are accepted as `duplicate: true` without re-enqueue wake. Stale `Processing` rows older than five minutes (by `ReceivedAt`) are reclaimed as `Failed` for retry (max 8 attempts).

## Phase 5 Entitlements and card access

Paid plan periods live in `user_plan_entitlements` (one `ACTIVE` per user). `users.PlanId` / `PlanActivatedAt` / `PlanExpiresAt` remain the live projection for purchase rules and admin overrides.

| Behavior | Rule |
|---|---|
| Lite / Love activate | Captured payment or admin activate writes an entitlement row and updates the user projection |
| Lite → Love | Prior active Lite is `SUPERSEDED`; Love becomes the sole `ACTIVE` row |
| Expiry | Active rows past `ExpiresAt` are lazily marked `EXPIRED` on plan resolve |
| Lite tickets | Counted per `card_unlocks.user_plan_entitlement_id` (atomic advisory lock); max 3 for Lite |
| Fresh Lite | A new entitlement does not inherit prior period ticket spend |
| `this_card` | Permanent `PURCHASE` unlock; survives plan expiry; Ask Back access stays unpaused |
| Free fallback | Expired plan unlocks pause; purchase unlocks and free top-reason ask-back keep working |
| Feature auth | Existing Ask Back access / unlock APIs evaluate the rules above (no parallel card-access route) |

## Phase 6 History, reconciliation, refunds

| API | Purpose |
|---|---|
| `GET /api/v1/payments/history` | Caller payment orders (paginated) |
| `GET /api/v1/payments/orders/{orderId}` | Owner-scoped order + attempts |
| `POST /api/v1/yakku/payments/attempts/{id}/refunds` | Admin Razorpay refund (access not revoked) |

Background `payment-reconciliation` worker (every 5 min): expires stale unpaid orders, fulfills gateway-captured pending orders, logs missing fulfillments and exhausted webhooks.

Operational checklist: [payments-ops.md](./payments-ops.md).
