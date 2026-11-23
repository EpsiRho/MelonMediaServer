# MelonLib

MelonLib is the shared library for [Melon](https://github.com/EpsiRho/MelonMediaServer), a media server for your own music library. You use it for two things:

- **Writing a plugin.** The plugin interfaces (`IPlugin`, `IHost`, `IArtworkProvider`) are in `Melon.Interface`.
- **Writing an app that talks to a Melon server.** `MelonLib.API` is a client for the server's API, and `Melon.Models` has the tracks, albums, artists, playlists and so on that it sends back.

## Making a Plugin

Melon already has MelonLib loaded, so your plugin should use Melon's copy instead of shipping its own. Leave the runtime assets out when you add the package:

```xml
<PackageReference Include="MelonLib" Version="1.3.3" ExcludeAssets="runtime" />
```

Then make a class that implements `IPlugin`. The [plugin tutorial](https://melon.docs.epsirho.com/Plugins/Introduction.html) walks through the whole thing.

## Talking to a Server

```csharp
using MelonLib.API;

// Point the client at your server. The user agent shows up as the device name.
await APIClient.Init("https://music.example.com", "My Melon App", debug: false);

// 0 means signed in, 1 means the server said no (usually a wrong password), 2 means it couldn't be reached.
if (await Auth.AuthenticateUser("epsi", "password") != 0) return;

// First page of tracks, sorted by name.
var tracks = await Search.Tracks(page: 0, count: 50);
foreach (var track in tracks) Console.WriteLine(track.Name);
```

Every endpoint is described in the [API docs](https://melon.docs.epsirho.com/api/EndpointsOverview.html).

## License

Apache-2.0, same as Melon. See LICENSE and NOTICE.
