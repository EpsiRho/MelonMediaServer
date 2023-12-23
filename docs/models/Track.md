# Track

## Model
|Type|Name|Notes|
|----|----|----|
|ObjectId|\_id|MongoDb's BSON ID|
|string |TrackId|The hex string representation of the track's ID|
|[ShortAlbum](models/ShortAlbum)|Album|Info about the track's album|
|int|Position|The position of the track in the album's order|
|int|Disc|The disc the track is on|
|string|Format|The track's format (.flac, .mp3, etc)|
|string|Bitrate|The bitrate of the file|
|string|SampleRate|The sample rate of the file|
|string|Channels|The number of channels|
|string|BitsPerSample|The bits per sample of the file|
|string|MusicBrainzID|The musicbrainz ID|
|string|ISRC|The track's ISRC|
|string|Year|The track's release year|
|string|TrackName|The name of the track|
|string|Path|The path to the track's file|
|string|Duration|The Duration of the track in milliseconds|
|long|PlayCount|The amount of times this track has been played|
|long|SkipCount |The amount of times this track has been skipped(Unimplemented)|
|float|Rating|The rating of the track (no official rating system has been decided)|
|int|TrackArtCount|The number of artworks the track contains|
|DateTime|LastModified|The last time the track's file was modified|
|DateTime|ReleaseDate|The release date of the track|
|List\<string\>|TrackGenres|The track's genres|
|List\<[ShortArtist](models/ShortArtist)\>|TrackArtists|Info on the track artists|

## Example
```
{
  "_id": {
    "timestamp": 1703299899,
    "machine": 13576624,
    "pid": -19299,
    "increment": 1603840,
    "creationTime": "2023-12-23T02:51:39Z"
  },
  "trackId": "65864b3bcf29b0b49d187900",
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
  "format": ".flac",
  "bitrate": "1676",
  "sampleRate": "44100",
  "channels": "2",
  "bitsPerSample": "24",
  "musicBrainzID": "b9c67809-6dd4-4c41-8ea6-15de962aaa2d",
  "isrc": "GBARL1401768",
  "year": "2022",
  "trackName": "Isometric (Intro)",
  "path": "Music\\Soundbites\\Madeon\\Adventure (Deluxe)\\01 - Madeon - Isometric (Intro).flac",
  "duration": "80567.41496598639",
  "playCount": 0,
  "skipCount": 0,
  "rating": 0,
  "trackArtCount": 1,
  "lastModified": "2023-04-29T21:19:49.539Z",
  "releaseDate": "2022-03-30T05:00:00Z",
  "trackGenres": [
    "Electronic",
    "Pop",
    "Dance-pop",
    "Electro House",
    "Synth-pop"
  ],
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