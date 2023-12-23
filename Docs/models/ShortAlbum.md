|Type|Name|Notes|
|----|-----|------|
|ObjectId|\_id|MongoDb's BSON ID|
|string|AlbumId|The Album's ID in a hex string representation|
|string|AlbumName|The name of the album|
|DateTime|ReleaseDate|The release date of the album|
|string|ReleaseType|The album's type (Album, Single, EP, Broadcast, Other)|
## Example
```
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
  }
```