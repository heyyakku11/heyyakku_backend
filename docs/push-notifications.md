# Push notifications: per-device permission

Whether push is allowed is a property of an app **installation** (a device row), not of a user. One user can allow it on
a phone and still have it off on a tablet they signed in on later.

## State on a device

| Field | Meaning |
| --- | --- |
| `NotificationPermission` | What the OS said: `unknown`, `granted` or `denied`. Reported by the app. |
| `IsPushAllowed` | Push is switched on for this installation. Starts off. |
| `PushToken` | The FCM token. Stored, but it never proves that notifications are allowed. |

Rules (enforced in `Device`, and by the check constraint `CK_devices_push_allowed_requires_granted`):

- A new or signed-in device starts with permission `unknown` and `IsPushAllowed = false`.
- A newly **granted** permission switches `IsPushAllowed` on; any other permission (unknown, denied) switches it off.
- `IsPushAllowed` can be set explicitly (the in-app switch), but only to `true` while the permission is granted.
- A **denied** permission clears the token.
- A token refresh or a repeated "granted" does not change the switch, so an in-app "off" survives them.
- Logging out, being evicted, or another account signing in on the installation resets it (the installation is released,
  its token is cleared). Other devices of the same user are untouched.

## A device receives push only when

it belongs to a user, is active, has a token, has permission `granted` and `IsPushAllowed`, and its current session is
a live session of that user (`DeviceRepository.PushEligible`). The check is repeated when sending.

## API

- `POST /api/v1/auth/verify` creates the device from `device: { installationId, platform }`. The response's
  `user.isPushAllowed` reports the signed-in installation, so it is `false` on a fresh sign-in.
- `PATCH /api/v1/devices/me` (header `X-Installation-Id`) sets `notificationPermission`, `pushToken`, metadata and
  `isPushAllowed`.
- `GET /api/v1/devices/me` returns the current device, including `notificationPermission`, `isPushAllowed` and
  `pushEnabled` (whether push can be delivered right now).
- Removed: `PATCH /api/v1/users/push` and `isPushAllowed` in `GET /api/v1/users`. The account-level switch no longer
  exists.
- Deprecated: `POST /api/v1/devices/register`.

## Migration `MakePushAllowedPerDevice`

The old `user_profiles.IsPushAllowed` is dropped. It is not copied to every device: a device is switched on only if it
last reported a **granted** permission **and** its user had not switched push off. Unknown or denied permissions, users
who opted out, and guest or unowned devices start off, and the app turns them on again when it reports a granted
permission. `Down` restores the column, marking a user opted out when they have granted devices and none is on.
