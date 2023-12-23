# ShortPlaylist
The info class of a playlist. Does not contain it's tracks, get those from [playlists/get-tracks](playlists/get-tracks).
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


## Example
```
{
  "_id": {
    "timestamp": 1703328475,
    "machine": 9176092,
    "pid": -1113,
    "increment": 12751606,
    "creationTime": "2023-12-23T10:47:55Z"
  },
  "playlistId": "6586badb8c041cfba7c292f6",
  "name": "Good Songs",
  "description": "",
  "trackCount": 2
  "owner": "Epsi",
  "editors": [],
  "viewers": [],
  "publicViewing": false,
  "publicEditing": false,
  "artworkPath": ""
}
```