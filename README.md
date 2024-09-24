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
See the [Installation Guide](https://melon.docs.epsirho.com/Guides/Installation.html)!

### Clients
There are currently no clients for Melon, but two are planned. 
- Galia, the Windows application written with the WASDK.
	- Galia's code is currently closed during early dev, but will be up on GitHub soon.
- Watermelon, the iOS application written with Swift.

Work has already begun on the windows application. When I have completed the Galia and Watermelon apps, I will work on more first-party apps as I can, hoping to get apps on MacOS, AppleTV, and Android. I personally do not want to work on a Web App, I just dislike working in JS, so there will likely not be one first-party.

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
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)
	- Windows Tray Icon Support
- [MongoDb](https://www.mongodb.com/)
	- Used as Melon's database.
- [NAudio](https://github.com/naudio/NAudio) and [NAudio.Lame](https://github.com/Corey-M/NAudio.Lame)
	- Used for transcoding
- [Pastel](https://github.com/silkfire/Pastel)
	- Used to color the console UI
- CaydoFox - 3D Melon Icon
- Hypervis0r - Help with authentication
- KindaConfusion - Testing, Bug fixes, Various Code Contributions