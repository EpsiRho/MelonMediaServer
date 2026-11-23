# Melon Media Server

## About
Melon is a media server that scans your music files and lets you stream them. This project is focused on including lots of customization and functionality to make this the best way to view, manage, and listen to your local music library.

See the [Melon Docs](https://melon.docs.epsirho.com) for more info.

### Standout features
- Shuffle Modes:
	- Shuffle your music randomly, prioritize your favorite tracks, prioritize track discovery, group tracks by album, and group tracks by artist.
	- Track Links keep tracks that flow back to back next to each other in shuffled queues (For example: Nirvana -> Mania from Madeon's Good Faith album).
	- See the [Shuffling](https://melon.docs.epsirho.com/Guides/Shuffling.html) Guide for more info.
- Play Statistics:
	- Get in-depth music listening stats including your favorite tracks, genres, and times to listen.
- Multiple Queues:
	- Melon keeps track of queues on all your devices. So you can pick up where you left off when moving to a different device, control queues playing on other devices, and save queues for later.
	- Queues are removed after a controllable time (Default 48 hours), unless they are favorited.
- Multiple Users
	- An invite code allows your friends and family to sign up on your server and listen along with their own stats, ratings, playlists, and user page.
- Plugin support
	- Melon has in-depth plugin interfaces for easy access to making powerful plugins.

## Get Started Using Melon
**[Download Melon](https://melon.docs.epsirho.com/Download.html)**: run MelonInstaller and it installs the latest release, then opens MelonGUI (or MelonCLI, when run from a terminal). See the [Installation Guide](https://melon.docs.epsirho.com/Guides/Installation.html).

Releases are tagged by branch and version, like `Beta-0.9.0.0`. There is a Beta branch today. A Stable branch follows once a Beta build is promoted.

| Folder | What it is |
|---|---|
| `Server` | MelonWebApi, the server |
| `Controller` | MelonGUI, the Windows app for setup and administration |
| `CLI` | MelonCLI, the terminal app for Windows, Linux and macOS |
| `Installer` | MelonInstaller, for updates and switching branches |

In MelonCLI, use Tab to switch between sections and actions, the arrow keys to move, Enter to select, and Esc to go back. Use `--status` to print JSON for a running server or `--dataPath DIRECTORY` to select another Melon profile. Local controllers and server plugins manage the running server through the [administration interface](docs/Architecture/LocalControllers.md).

### Clients
- [Galia](https://github.com/EpsiRho/Galia), the Windows application written with the WASDK.
- Watermelon, the iOS application written with Swift. It will arrive through TestFlight.

When I have completed the Galia and Watermelon apps, I will work on more first-party apps as I can, hoping to get apps on MacOS, AppleTV, and Android. I personally do not want to work on a Web App, I just dislike working in JS, so there will likely not be one first-party.

### Where to start building apps
All endpoints require authentication except for logging in, so [auth](https://melon.docs.epsirho.com/api/Authorization.html) is a good place to start. 

Once you've obtained a JWT, a good way to test the API is with [search](https://melon.docs.epsirho.com/api/Search.html) endpoints.

The rest of the endpoints can be found at the [Endpoints Overview](https://melon.docs.epsirho.com/api/EndpointsOverview.html) and models can be found at [Models Overview](https://melon.docs.epsirho.com/models/modelsOverview.html).

### Where to start building plugins
Check out the [Plugins Introduction](https://melon.docs.epsirho.com/Plugins/Introduction.html) page.

### User Guides
We provide guides on how different parts of melon work and how to use them:
- [Configure Melon](https://melon.docs.epsirho.com/Guides/Configure.html)
	- A guide on the different settings and launch arguments available.
- [Installation Guide](https://melon.docs.epsirho.com/Guides/Installation.html)
	- Info on how to install Melon for the first time
- [Playlists](https://melon.docs.epsirho.com/Guides/Playlists.html)
	- Info on how playlists and collections work.
- [Search](https://melon.docs.epsirho.com/Guides/Search.html)
	- Info about search filters and sorting
- [Shuffling](https://melon.docs.epsirho.com/Guides/Shuffling.html)
	- Info about Melon's Shuffle modes.
- [Users](https://melon.docs.epsirho.com/Guides/Users.html)
	- A guide on the different user types and how to invite new users

### Architecture Information
There are a couple docs about how melon handles certain things behind the scenes or contain more info on how to use APIs:
- [Artwork](https://melon.docs.epsirho.com/Architecture/Artwork.html)
	- Information about how melon handles and stores images for track/album/artist/etc.
- [MelonInstaller](https://melon.docs.epsirho.com/Architecture/MelonInstaller.html)
	- Documentation of the exe/dll MelonInstaller, which is used for installs, updates, and building releases of Melon.
- [Melon Scanner](https://melon.docs.epsirho.com/Architecture/MelonScanner.html)
	- Details about how the Scanner scans files and creates the database.
- [WebSockets](https://melon.docs.epsirho.com/Architecture/WebSockets.html)
	- Melon uses WebSockets to give clients Realtime updates, allowing clients to control each other.


### Extra Credits
- [ATL](https://github.com/Zeugma440/atldotnet)
	- Used for reading track metadata
- [MongoDb](https://www.mongodb.com/)
	- Used as Melon's database.
- [NAudio](https://github.com/naudio/NAudio) and [NAudio.Lame](https://github.com/Corey-M/NAudio.Lame)
	- Used for transcoding
- CaydoFox - 3D Melon Icon
- Hypervis0r - Help with authentication
- KindaConfusion - Testing, Bug fixes, Various Code Contributions

## License
Apache-2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
