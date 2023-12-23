#  ShortTrack

## Model
|Type|Name|Notes|
|----|------|-----|
|ObjectId|\_id|MongoDb's BSON ID|
|string|TrackId|The ID of the track|
|[ShortAlbum](models/ShortAlbum)|Album|Info about the album|
|int|Position|The position of the track in it's album order|
|int|Disc|The disc this track is on|
|string|TrackName|The name of the track|
|string|Duration|The length of the track in milliseconds|
|int|TrackArtCount|The number of artworks this track contains|
|string|Path|The track's path|
|List\<[ShortArtist](models/ShortArtist)\>|TrackArtists|A list of artist info|
## Example
```
{
  "_id": {
	"timestamp": 1703296729,
	"machine": 5705263,
	"pid": 287,
	"increment": 2175664,
	"creationTime": "2023-12-23T01:58:49Z"
  },
  "trackId": "65863ed9570e2f011f2132b0",
  "album": {
	"_id": {
	  "timestamp": 1703296729,
	  "machine": 5705263,
	  "pid": 287,
	  "increment": 2175663,
	  "creationTime": "2023-12-23T01:58:49Z"
	},
	"albumId": "65863ed9570e2f011f2132af",
	"albumName": "Adventure",
	"releaseDate": "2022-03-30T05:00:00Z",
	"releaseType": "album"
  },
  "position": 1,
  "disc": 1,
  "trackName": "Isometric (Intro)",
  "duration": "81",
  "trackArtCount": 1,
  "path": "Music\\Soundbites\\Madeon\\Adventure (Deluxe)",
  "trackArtists": [
	{
	  "_id": {
		"timestamp": 1703296728,
		"machine": 5705263,
		"pid": 287,
		"increment": 2175650,
		"creationTime": "2023-12-23T01:58:48Z"
	  },
	  "artistId": "65863ed8570e2f011f2132a2",
	  "artistName": "Madeon"
	}
  ]
}
```