# Playlist

**This model will not be sent back to you!** Normally you'll be sent a [ShortPlaylist](models/ShortPlaylist). Since playlists can hold a lot of tracks, you'll get those tracks through a different endpoint than you use to get the playlist info. 
- Viewing playlist info
	- [playlists/get](/playlists/get)
	-  [playlists/search](/playlists/search)
- Viewing playlist tracks
	-  [playlists/get-tracks](/playlists/get-tracks)

## Model
|Type|Name|Notes|
|----|----|-----|
|ObjectId|\_id|MongoDb's BSOn ID|
|string|PlaylistId|The hex string representation of the playlist's ID|
|string|Name|The name of the playlist|
|string|Description|The playlist description / bio|
|long|TrackCount|The number of tracks in this playlist|
|string|Owner|The username of the owner|
|List\<string\>|Editors|Usernames of users allowed to edit this playlist|
|List\<string\>|Viewers|Usernames of uses allowed to view and listen to this playlist|
|bool|PublicViewing|Allow anyone to see this playlist (false by default)|
|bool|PublicEditing|Allow anyone to edit this playlist (false by default)|
|string|ArtworkPath|Path of the playlist artwork (Unimplemented)|
|List\<[ShortTrack](model/ShortTrack)\>|Tracks|The tracks in this playlist|

