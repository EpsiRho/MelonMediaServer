# Local administration

Run `MelonWebApi` to host the backend, WebAPI, and WebUI. The host owns settings, database maintenance, scans, and plugin execution. Closing a controller leaves the host running. On Windows, open MelonGUI for first-time setup, recovery, and administration. Setup can use an existing MongoDB or install a dedicated, authenticated local Windows service.

A .NET controller references MelonLib and connects under the same OS account as the host. Trusted server plugins use the corresponding typed methods on `Host.Administration`. Existing authenticated HTTP routes for scans, settings, backups, updates, and plugin administration use the same backend operations.

```csharp
using Melon.Interface;
using MelonLib.API;

var client = new LocalControllerClient();
var status = await client.CallAsync(nameof(IMelonAdministration.GetStatus));
await client.CallAsync(nameof(IMelonAdministration.SetQueueCleanupHours), new { hours = 48 });
await foreach (var notice in client.SubscribeAsync(cancellationToken))
    Console.WriteLine($"{notice.Sequence} {notice.Topic} {notice.Message}");
```

## Setup and recovery

Local management is available when MongoDB or the web listener is unavailable. Read `GetStatus` and `GetSettings` first. To change databases, call `SetMongoConnection` with a reachable connection string and then `Restart`. Reconnect to the pipe after a restart. New profiles listen on `http://localhost:14524` until the operator configures networking. Listening URL and certificate changes also take effect on restart. Connection details in settings show server addresses and omit credentials.

When `setupRequired` is true, use `CreateFirstAdmin` and `SetLibraryPaths`. Use `ConvertDatabase` to approve a pending conversion. It takes a backup first. For desktop actions such as opening the Melon folder, use `GetStatus().DataDirectory` in the controller's user session.

## Accounts and listening history

Use `GetUsers` to list account IDs, names, and roles. An operator can issue a ten-minute, single-use password reset with `CreatePasswordReset`. The user opens `/auth/reset-password` on their server, or a client posts JSON containing `code` and `password` to that address. Issuing a replacement code invalidates the previous one for that account. Changing the password or deleting the account invalidates its login tokens.

`CreateInvite` defaults to ten minutes. `GetInvites` returns the active codes and their UTC expiry, and `RevokeInvite` invalidates one. Codes are held in memory. A controller QR encodes just the code; Galia reads it in Sign up, and `POST auth/register` redeems it.

`GetListeningSummary` returns daily UTC play/skip counts, listening duration for plays, and the five most-played tracks. An empty user ID includes every account, including retained history from deleted accounts. `PruneDeletedUserStats` removes those accounts’ listening events, play and skip counts, and ratings across all dates.

## Wire protocol

Use the pipe name returned by `LocalControllerClient.GetPipeName()`. For a custom `--dataPath`, pass that same directory to the client. The name is `Melon.Control.v1.` followed by the first 24 uppercase hex digits of the SHA-256 of the absolute data directory in UTF-8. Trim the trailing directory separator, preserving filesystem roots, and uppercase the path on Windows. The transport is a .NET named pipe on Windows and a .NET named pipe backed by a Unix domain socket on Unix.

Send one UTF-8 JSON line per connection. Method and parameter names match the public administration interface. Replies have either `ok: true` and a `result`, or `ok: false` and an `error`. A successful void command returns a null result. [Administration operations](../../Code/MelonLib/Interface/IMelonAdministration.cs)

```json
{"method":"StartScan","params":{"skip":true}}
```

`Subscribe` keeps its connection open. The first line contains a status snapshot. Subsequent lines carry a sequence number, topic, and message. Each controller receives its own stream. Read `GetStatus` for current scan counts and `GetSettings` for configuration. A slow subscriber can miss older messages after its 128-entry buffer fills. On a sequence gap or reconnect, refresh the relevant snapshots. Plugins can broadcast notices through `Host.Administration.PublishMessage`.

## Plugin controls

A plugin can implement `IControllerPlugin` to describe fields and named actions. A controller calls `GetPluginPage`, renders the fields, and sends edits through `InvokePlugin`. The service executes the handler. Port legacy console menu callbacks to this interface. [Demo plugin](../../Code/Demos/Demo%20Plugin/MelonTestPlugin/EventPlugin.cs)
