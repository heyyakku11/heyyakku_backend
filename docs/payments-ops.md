# Payments — operations & staging checklist

Do **not** switch production Razorpay credentials or run live charges from this checklist.

## Staging checklist

1. Apply migrations (`payment_orders` … `payment_refunds`).
2. Set Staging env:
   - `RAZORPAY_ENABLED=true`
   - `RAZORPAY_KEY_ID` / `RAZORPAY_KEY_SECRET` / `RAZORPAY_WEBHOOK_SECRET` (Test Mode keys only)
   - `RAZORPAY_BASE_URL=https://api.razorpay.com/v1` (optional)
   - Confirm `Payment:*` in `appsettings` (Provider/Currency/CaptureMode/OrderExpiryMinutes)
3. In Razorpay Dashboard (Test Mode), point the webhook to `https://<staging-host>/api/v1/webhooks/razorpay` with events: `payment.captured`, `order.paid`, `payment.authorized`, `payment.failed`.
4. Confirm `Razorpay:Enabled` is false in Development so local boots without secrets.
5. Smoke:
   - Create Lite order → Checkout Test Mode → `POST /api/v1/payments/verify`
   - Confirm `GET /api/v1/payments/history` and `GET /api/v1/payments/orders/{id}`
   - Confirm Ask Back access after capture; confirm webhook path also fulfills when verify is skipped
6. Confirm reconciliation worker logs appear (`payment-reconciliation`, every 5 minutes).
7. Admin refund smoke (optional): `POST /api/v1/yakku/payments/attempts/{attemptId}/refunds` — access must remain until a separate clawback.

## Reconciliation behaviour

| Finding | Action |
|---|---|
| Created/Pending past `ExpiresAt` | Mark `EXPIRED` + payment log |
| Pending/recently-expired with Razorpay captured payment | Idempotent fulfillment (amount/currency must match) |
| Paid without entitlement/unlock | Log `PaymentReconciliationMismatch` (no blind grant) |
| Webhook retries exhausted (≥ 8) | Log for manual review |

Never grant access from local status alone without a captured Razorpay payment.

## Refunds (business rule)

Admin refunds call Razorpay and store `payment_refunds`.

**Refunds do not automatically revoke** plan entitlements or permanent `this_card` unlocks. If product requires clawback, use a separate explicit admin action and document it in the change ticket.

## Useful endpoints

| Method | Path | Auth |
|---|---|---|
| POST | `/api/v1/payments/orders` | User JWT |
| POST | `/api/v1/payments/verify` | User JWT |
| GET | `/api/v1/payments/history` | User JWT |
| GET | `/api/v1/payments/orders/{orderId}` | User JWT (owner) |
| POST | `/api/v1/webhooks/razorpay` | Anonymous + HMAC |
| POST | `/api/v1/yakku/payments/attempts/{id}/refunds` | Admin |

## Monitoring

- Prefer `payment_logs` for financial audit (not subject to system-log retention).
- Correlate with `correlationId`, `payment_order_id`, Razorpay order/payment ids.
- Never log key/webhook secrets or PAN/CVV/UPI PIN.
