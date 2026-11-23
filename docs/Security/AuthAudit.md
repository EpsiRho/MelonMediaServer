# Melon security audit: authentication, sign-up, QR sign-in, input limits

Code audit of `MelonWebApi` + `Melon` (branch `final-push`) and the Galia client, November 2026. Nothing was tested against
a live server. Severity reflects impact × how reachable it is today.

## Summary

| # | Finding | Severity | Who can trigger it |
|---|---|---|---|
| 1 | Art uploads build the file path from the client's `Content-Type` → write any file anywhere → drop a DLL in `Plugins/` → code execution | **Critical** | Anyone holding an admin token |
| 2 | Public user page injects username / bio / track names into a `<script>` unescaped → stored XSS on the API's own origin | **High** | Any user (via bio), anyone who can get a crafted tag into the library |
| 3 | Passwords travel in URLs (`auth/login`, `users/create`, `users/change-password`) → server, proxy and CDN logs, plugins | **High** | Passive: anyone who can read logs on the path |
| 4 | No rate limiting or lockout on login / invite / reset | **High** | Anyone on the internet |
| 5 | JWTs accepted from `?jwt=` on every endpoint; Galia puts them in every media/art URL | **Medium** | Passive: log readers |
| 6 | Changing a password needs no current password; a stolen token = permanent account takeover | **Medium** | Anyone with a user's token |
| 7 | Usernames aren't unique (create / rename) → login picks "the first match", impersonation/lockout | **Medium** | Any user (rename), any invitee |
| 8 | Raw user input used as MongoDB regex (`device`, `name`, `username`), some endpoints unauthenticated → ReDoS on the database | **Medium** | Anyone (user-page endpoints) |
| 9 | WebSocket messages have no size limit; state relayed verbatim | **Medium** | Any signed-in user, including `Pass` |
| 10 | Request sizes only bounded by Kestrel's 30 MB default; `count` unbounded on searches; art `size` resizes on unauthenticated endpoints | **Medium** | Any user / anyone |
| 11 | QR sign-in encodes server + username + **plaintext password** | **Medium** | Anyone who sees or photographs the code |
| 12 | Signing key is random per process → every restart signs everyone out → clients store passwords to re-login | **Design** | n/a: drives #3 / #11 |
| 13 | Smaller items: see "Other findings" | Low | various |

Things that are already done right: PBKDF2-SHA512 with 350k iterations and per-user salt; fixed-time compare; session
stamp (a password change revokes outstanding JWTs, re-checked every request); invite codes are 80-bit random,
single use, capped at 25; signup tickets are single use; reset codes are 128-bit, stored hashed, 10-minute; backups
restore only from an allow-list; playlist XML parsing prohibits DTDs (no XXE); updates verify a SHA-256 digest;
playlist / queue / collection endpoints all check owner / editor / viewer; cast control is scoped to the caller's devices;
track metadata updates can't change `Path` / `LyricsPath`.

---

## Findings in detail

### 1. Arbitrary file write → code execution (Critical)
`ArtUploadController` (all 6 upload actions except track art):
```csharp
var contentType = image.ContentType.Replace("image/", "");
var filePath = $"{StateManager.melonPath}/Assets/defaultArtwork.{contentType}";
using (var stream = new FileStream(filePath, FileMode.Create)) ...
```
`Content-Type` is whatever the client sends. `image/x/../../Plugins/evil.dll` writes `<melonPath>/Plugins/evil.dll`
(Windows normalizes `..` lexically, so the bogus `defaultArtwork.x` directory doesn't need to exist). `POST
api/settings/plugins/rescan` then loads it (`PluginsManager` loads every `*.dll` in that folder) → arbitrary code as the
service account. It's admin-only today, but the backlog wants playlist art opened to owners, and admin tokens are
exposed by #2, #3 and #5. Also: `fileStream.Read(bytes, 0, len)` may read short; and the bytes are never checked to be an image.

**Fix:** decode the upload with SkiaSharp (already referenced), reject if it isn't an image, re-encode to JPEG/PNG, and
pick the extension from the decoded format. Never put client text in a path. Add a `SafePath(baseDir, name)` helper
(`Path.GetFullPath` + "must start with `baseDir`") and use it for every file write/read built from input.

### 2. Stored XSS on `/user-page` (High)
`UserPagesController.UserPage` (no auth) builds JavaScript by string interpolation:
`bio: "{user.Bio}"`, `username: "{user.Username}"`, track/album/artist names. A bio of `"};alert(1);//` or
`</script><script>…` runs on the server's origin, which also hosts Swagger UI and the password-reset page. Track/album
names come from file tags, so a crafted file in the library does the same.

**Fix:** serialize the data with `System.Text.Json` (it escapes `<`, `>`, `&`, quotes) or serve it from a JSON endpoint
the page fetches; add `Content-Security-Policy: default-src 'self'; script-src 'self'` (move the inline vars out).
Cap bio length (e.g. 1,000 chars).

### 3. Credentials in URLs (High)
- `GET auth/login?username=&password=`
- `POST api/users/create?username=&password=`
- `PATCH api/users/change-password?password=`

Query strings land in reverse-proxy / CDN / Kestrel logs, browser history, and `PluginMiddleware` hands every query
parameter of every authenticated request to every plugin.

**Fix:** credentials only in the body (JSON) or `Authorization: Basic` for the token exchange, all `POST`. See the plan.

### 4. No brute-force protection (High)
Unlimited `auth/login`, `auth/code-authenticate`, `auth/reset-password` attempts. Unknown usernames skip PBKDF2, so
response time reveals which usernames exist.

**Fix:** ASP.NET Core rate limiter (built in, no new package): per IP and per username on the auth endpoints
(e.g. 5/min, then exponential backoff), plus a dummy PBKDF2 when the user doesn't exist. Emit failed-login events for plugins.

### 5. Tokens in query strings (Medium)
`OnMessageReceived` accepts `?jwt=` on **every** endpoint. Galia adds it to media and art URLs (`MelonClient.AuthedUrl`),
so 60-minute full-power tokens end up in logs. The cast WebSocket authenticates once and stays open after the token
expires or the password changes.

**Fix:** accept query tokens only on media/art routes and only with `aud=media` (short-lived, read-only, see plan). Re-check the
session on WebSockets periodically (or on the existing ping) and close revoked ones.

### 6. Password change without the current password (Medium)
`change-password` needs only a valid token for that user. A stolen token (60 min) becomes permanent takeover, and the
real owner gets locked out.

**Fix:** require `currentPassword` (or a re-auth within the last 5 min); revoke all other sessions on change.

### 7. Duplicate usernames (Medium)
`users/create` and `users/change-username` never check uniqueness; login takes `users[0]`. A user renaming to an
admin's name can lock the admin out (if their document sorts first) and confuses every "find by username".
`change-username` also accepts empty strings.

**Fix:** unique index on `Username` with case-insensitive collation; validate 3–32 chars, letters/digits/`._-`.
Validate `role` against `Admin | User | Pass` in create/update.

### 8. Regex injection (Medium)
`new BsonRegularExpression(input, "i")` without `Regex.Escape` in: `StatsController` / `UserPagesController` (`device`,
7 + 7 endpoints, the user-page ones unauthenticated), `PlaylistsController` / `CollectionsController` / `QueuesController`
(`name`), `UserController.search` (`username`). A pattern like `(a+)+$` pins MongoDB's CPU.

**Fix:** `Regex.Escape` everywhere (filters in `MelonAPI` already do this), plus length caps.

### 9. WebSocket limits (Medium)
`StreamManager.ReceiveAsync` concatenates frames until `EndOfMessage` with no cap; `SET STATE:` stores and relays the
payload verbatim to watchers. One client can exhaust server memory. Command matching uses `Contains`, so
`"xSET DEVICE:y"` also matches.

**Fix:** cap messages (64 KB), close on overflow; rate-limit; parse commands with exact prefixes.

### 10. Size and count limits (Medium)
- No explicit `MaxRequestBodySize`: Kestrel's 30 MB default applies everywhere, including JSON `PUT api/update/*` and playlist import.
- `count` / `page` unbounded on search & list endpoints (`count=100000000` returns the whole library; `page*count`
  overflows).
- `api/download/*-art?size=` is unauthenticated for track/album/artist art, and every distinct size (16–2048) is
  a fresh decode + encode: an easy CPU and disk-churn lever (cache is capped at 512 MB, so no disk exhaustion).
- `AllowSynchronousIO = true` lets slow uploads tie up thread-pool threads.

**Fix:** global limit around 1 MB, per-endpoint `[RequestSizeLimit]` (art 20 MB, import 2 MB, metadata 1 MB); `count ≤ 500`;
snap `size` to a few buckets (64/128/256/512/1024) and rate-limit anonymous art; turn off synchronous IO (fix the
`Stream.Read` art code to async at the same time).

### 11. QR sign-in leaks the password (Medium)
Galia `SettingsPage.ShowQr` renders `server\nusername\npassword`. Anyone who sees the screen has the password forever.
Galia also accepts a scanned login QR for any server URL, including `http://`.

**Fix:** device-link codes, see the plan.

### 12. Per-process signing key (design)
`StateManager.JWTKey = Security.GenerateSecretKey()` at start-up, in memory only. Every restart invalidates all tokens, which is
why Galia keeps passwords (DPAPI) to log in again silently. Fine as a kill-switch, bad as the only session model.

**Fix:** persistent key protected with ASP.NET Data Protection (already used for the SSL config), with a `kid` for
rotation; refresh tokens for long sessions.

### 13. Other findings (Low)
- `user-page/listening-time` ignores `userId` and returns server-wide listening by hour, unauthenticated.
- `user-page` shows bio / favorites of any user regardless of `PublicStats`.
- `download/playlist-art` and `collection-art` don't check the playlist is visible to the caller.
- `auth/qr` turns arbitrary text into an SVG for anyone (harmless, but an open generator); it should render only invites.
- Swagger UI at `/` on production; consider `/swagger` behind a setting.
- `RequireHttpsMetadata = false`, no HSTS, default listener is `http://`. Add an "HTTPS required for non-local clients"
  setting plus HSTS; Galia should warn on `http://` non-LAN servers.
- JWT: no `iss`/`aud`, default 5-minute clock skew, admin-set lifetime unbounded. Add issuer/audience, 1-minute skew, and cap
  the lifetime.
- Role checks use string lists such as `"Admin, User"` (with a space). ASP.NET trims them so they work, but define constants/policies.
- Galia stores the JWT in `settings.json` in plain text (low: the user profile is the trust boundary); move it next to the
  refresh token in the DPAPI store.
- Run the Windows service as a dedicated low-privilege account (not LocalSystem), with write access only to `melonPath` and
  read-only access to libraries. That limits the damage of any future code execution.

---

## Plan

Three phases. Phase 0 changes no client protocol and can ship on its own; Phase 1 is the new auth API with a
compatibility window; Phase 2 is hardening.

### Phase 0: fix now (server only, no protocol change)
1. Art uploads: decode → re-encode → extension from format; `SafePath` helper used by every path built from input (#1).
2. User page: JSON-encode injected data, CSP header, bio cap (#2).
3. `Regex.Escape` on all regex inputs (#8).
4. WebSocket message cap + exact command parsing (#9).
5. Request size limits, `count` caps, art `size` buckets, `AllowSynchronousIO = false` (#10).
6. Rate limiting on `auth/*` + dummy hash for unknown users (#4).
7. Unique username index, username/role validation (#7). Migration: on start-up, report duplicates and refuse to create the index
   until an admin renames them (via the local controller).
8. Small items: listening-time userId, playlist-art visibility, `auth/qr` invite-only (#13).

#### Phase 0 status (done, uncommitted, not deployed)
| Item | What changed |
|---|---|
| #1 art uploads | `Services/ImageUpload.cs`: upload must decode (SkiaSharp), ≤ 20 MB, ≤ 100 MP; turned upright, capped at 4096 px, re-encoded to JPEG (PNG if transparent); extension comes from the re-encode. `SafePath.Combine` guards every art write, read and delete built from a stored name. Default art is always written as `defaultArtwork.jpg` (the name the server reads; uploads under other extensions were never used before). |
| #2 user page | Removed: `UserPagesController` deleted (all `/user-page*` endpoints, including the unauthenticated stats ones). The `wwwroot` page assets are left for the future social-features rework. |
| #8 regex | `Regex.Escape` on `name` (playlists, collections, queues), `device` (stats), `username` (user search). |
| #9 WebSocket | 64 KB message cap (close with MessageTooBig), 50 messages/s per socket (close), commands matched exactly (`==` / `StartsWith("CMD:")`), device names and queue ids ≤ 128 chars, values taken after the first `:` (names with colons no longer truncated). |
| #10 sizes | Kestrel body limit 1 MB; art uploads 20 MB, playlist import 2 MB (extension must be a plain `.abc`); `count` clamped to 0–5000 and `page` to non-overflowing values on every endpoint (clamped, not refused: Galia's stats sync asks for 2,000); art `size` snaps up to 64/128/256/512/1024/2048; `AllowSynchronousIO` off (plugin middleware now writes async). |
| #4 brute force | Built-in rate limiter: 20/min per address on `auth/login`, `auth/code-authenticate`, `auth/qr`, `POST auth/reset-password`; 300/min per address on art downloads without a token (signed-in callers unlimited). Per username+address backoff after 5 wrong passwords (15 s doubling to 15 min, 429 + Retry-After). Unknown usernames now run a dummy PBKDF2. `X-Forwarded-For` honoured from loopback proxies only, so a local tunnel doesn't put every client in one bucket. |
| #7 usernames | `UserAccounts`: 2–32 chars, letters/digits/space/`._-`, unique ignoring case (409), checked on create and rename; case-insensitive unique index created at start-up (duplicates are logged instead); role must be Admin/User/Pass; bio ≤ 1,000; password ≤ 1,024. |
| #13 small | `auth/qr` no longer draws anything (removed in Phase 1, see below); invite lifetime capped at 30 days (Galia's max); playlist/collection art respects visibility; plugins no longer receive `password`/`jwt`/`code`/`token` query values. |

Not in Phase 0 (needs Phase 1): passwords/tokens still accepted in URLs, password change without the current password, WebSocket token revalidation, QR password.

### Phase 1: new auth API (server + Galia, old endpoints kept for one release)

Server advertises it so clients can switch safely: generalize `api/search/features` into `GET api/server/features`
(anonymous, returns e.g. `["stat-filters","auth-v2"]`). Galia uses v2 when it's listed, the old flow otherwise.

**Sessions and tokens**
- `Sessions` collection: `_id`, `UserId`, `DeviceName`, `CreatedUtc`, `LastUsedUtc`, `ExpiresUtc` (sliding 60 days),
  `RefreshHash` (SHA-256 of the token), `FamilyId`, `Revoked`.
- Access token: JWT, 15 minutes, claims `sub`, `role`, `sid` (session id), `melon_session` (password stamp), `iss`, `aud=api`.
  `OnTokenValidated` also checks the session isn't revoked (cache ~30 s).
- Refresh token: 256-bit random, opaque, single use. **Rotation with reuse detection:** each refresh issues a new one; presenting an
  already-used token revokes the whole family (it was stolen).
- Persistent signing key via Data Protection, with `kid`; admin "sign everyone out" rotates it.

**Endpoints**
| Endpoint | Body | Returns |
|---|---|---|
| `POST auth/token` | `Authorization: Basic base64(user:pass)` (or JSON `{username,password}`), `{deviceName}` | `{accessToken, refreshToken, expiresIn, user}` |
| `POST auth/refresh` | `{refreshToken}` | new pair |
| `POST auth/logout` | `{refreshToken}` | 204, revokes that session |
| `GET api/users/sessions` / `POST api/users/sessions/revoke` | `{sessionId}` | list / 204 (own sessions; admin: anyone's) |
| `POST auth/media-token` | none (bearer) | `{token, expiresIn: 600}`: `aud=media`, read-only, the only token accepted in `?jwt=` |
| `PATCH api/users/password` | `{currentPassword, newPassword}` | 204; revokes all other sessions |
| `PATCH api/users/username` | `{username}` | 204; validated, unique |

**Sign-up**
- `POST auth/invites` (admin) → `{code, url, expiresAt}`, where `url = https://<server>/auth/join?code=<code>`. The QR
  encodes that URL, so scanning fills in **server and code**. Galia's `Qr.Parse` already handles `/auth/qr?code=`; add
  `/auth/join`. `GET auth/join?code=` (anonymous) returns `{serverName, expiresAt, valid}` so the client can show
  "Join *Server* (expires in 8 min)" before asking for details. Opening the URL in a browser could show a small join page.
- `POST auth/register` `{inviteCode, username, password, deviceName}`: one call. It redeems the invite atomically,
  validates the username (unique, charset) and password (min 8, max 1024), and returns tokens directly. This replaces
  `code-authenticate` + `users/create?username&password`, so the 10-minute "Server" JWT goes away.

**QR sign-in (device link)**
- Signed-in device: `POST auth/device-link` → `{code, url, expiresAt}`. The code is 128-bit, single use, valid 2 minutes, and
  stored hashed. The QR shows `https://<server>/auth/link?code=…`. **No password or long-lived token in the code.**
- New device: `POST auth/device-link/redeem` `{code, deviceName}` → tokens for a **new session** that shows up in the
  sessions list and can be revoked. The showing device can poll `GET auth/device-link/{id}` to say "Signed in on <device>".
- Optional stricter variant later: the new device shows the code and the signed-in device scans and approves it (the secret
  never comes from the trusted screen). Same endpoints, roles swapped.

**Galia changes**
- `MelonClient`: `Login` → `POST auth/token`; `CreateUserWithServerJwt` → `auth/register`; refresh on 401 using the refresh
  token (replaces `_reauth` re-login with the stored password); media and art URLs use a cached media token, renewed
  around 8 minutes.
- `CredentialStore`: keeps the **refresh token** (DPAPI) instead of the password. Migration: on first start with `auth-v2`,
  log in once with the stored password, save the refresh token, delete the password.
- Settings → Account: "Sign in another device" shows the device-link QR (no warning dialog needed any more); "Devices"
  lists sessions with revoke.
- `Qr.Parse`: add `/auth/join?code=` and `/auth/link?code=`; drop the plaintext `LoginPayload` after the compatibility release;
  warn when the scanned server is `http://` and not local.
- Change-password UI asks for the current password.

**Compatibility:** keep `GET auth/login`, `code-authenticate` and query-string `users/create` / `change-password` for one
release behind a `LegacyAuth` setting (default on, then off), and log a deprecation warning each time they're used.

#### Phase 1 status (done, uncommitted, not deployed; old sign-in removed)
Server:
| Area | What was built |
|---|---|
| Features | `GET api/server/features` (anonymous): `stat-filters`, `auth-v2`. |
| Sessions | `Melon/LocalClasses/AuthSessions.cs`, `Sessions` collection (TTL index 7 days after expiry). Access JWT 15 min with `melon_sid` + `melon_scope=api`. Refresh token opaque, single use, rotated; the previous token has a 2-minute grace for racing requests, and any other reuse revokes the session. Each session stores a password stamp, so a password change ends every refresh session except the one that changed it. |
| Signing key | Persistent, `Configs/AuthKey.json` protected with Data Protection, read per request (`IssuerSigningKeyResolver`). Clock skew 1 min. Controllers: "Sign out every device" rotates it and revokes all sessions. |
| Token rules | `Middleware/TokenRules.cs`: media tokens (`melon_scope=media`, 6 h) only on `[MediaAccess]` endpoints (the download controller); session tokens only in the Authorization header; tokens without a scope or session are refused; a session's tokens work only while it is active (30 s cache). The WebSocket closes when its session is revoked. |
| Endpoints | `auth/token` (Basic or JSON), `auth/refresh`, `auth/logout`, `auth/media-token`, `auth/register`, `auth/device-link` (+ `/redeem`, `/{id}`), `PATCH api/users/password` (current password required, throttled), `GET api/users/sessions`, `POST api/users/sessions/revoke`. Token, refresh and register are rate limited. |
| `auth/qr` | **Removed.** Drawing only live invite codes made it an oracle for whether a code exists. `auth/join` and `auth/link` serve a static page with a CSP that echoes nothing and checks nothing. Galia draws every QR code itself. |
| Old sign-in | **Removed** (no compatibility window; the only client was Galia). Gone: `GET auth/login`, `auth/code-authenticate` and the "Server" role token, `POST api/users/create`, `PATCH api/users/change-password`, `auth/qr`, the `LegacyAuth` setting and its controller switch, and `Security.GenerateJwtToken`. Tokens issued by the old login stop working. MelonLib's `Auth.AuthenticateUser` now uses `auth/token`. |
| Upload limits | `MaxArtUploadMegabytes` (20, 1–200) and `MaxPlaylistImportMegabytes` (2, 1–100), changeable only from the controllers. `[UploadLimit]` sets the body and multipart limits before model binding. |

Galia (`Core/MelonClient.Auth.cs`, `Core/Accounts.cs`, `Controls/Qr.cs`, `Pages/OobePage.cs`, `Pages/SettingsPage.cs`):
- Session sign-in only. A server without `auth-v2` gets "This Melon server is too old". The device name is "Galia on <PC name>".
- `CredentialStore` keeps only the refresh token (`rt:<account>`); passwords are never stored. On a 401, Galia
  refreshes; if that's refused, it removes the token and shows "Sign in again".
- One-time migration: an account saved with a password by an older Galia build trades it for a session at the next launch,
  then deletes it (or deletes it and asks to sign in again if it no longer works).
- Media token fetched at sign-in and renewed whenever it has less than 3 hours left. Audio and playlist/collection art URLs
  use it. The WebSocket sends the session token in the header only.
- Sign up uses `auth/register`. Invite links point at `/auth/join`.
- "Sign in with QR" shows a device-link code (`/auth/link?code=`): two minutes, single use, no password, shows
  "Signed in on X". The login screen redeems a scanned or pasted link. The old password QR (`LoginPayload`) is gone.
- Change password asks for the current one and keeps this device signed in. "Signed-in devices" lists sessions with
  sign-out buttons.

Differences from the plan: media tokens last 6 hours (not 10 minutes), because audio URLs are handed to the player and
art URLs are cached; there's no `PATCH api/users/username` (the existing rename endpoint is validated since Phase 0);
`auth/join` returns a static page rather than `{valid, expiresAt}` (that would be the same oracle as `auth/qr`).
Not done yet: the `http://` warning when scanning.

### Phase 2: hardening
- HTTPS-required setting for non-local clients, HSTS, `RequireHttpsMetadata`.
- Swagger behind a setting; CSP + `X-Content-Type-Options: nosniff` + `Referrer-Policy: no-referrer` on all responses.
- JWT `iss`/`aud`, 1-minute skew, lifetime caps.
- Service account guidance in the installer; audit events (login, failed login, session created/revoked, password
  change, role change) through the existing plugin event system.
- Security tests: path traversal on uploads, oversized bodies and WebSocket frames, regex payloads, refresh-token reuse,
  revoked-session WebSocket.

### Effort (rough)
| Phase | Server | Galia |
|---|---|---|
| 0 | 1–2 days | none |
| 1 | 3–5 days | 2–3 days |
| 2 | 1–2 days | small |
