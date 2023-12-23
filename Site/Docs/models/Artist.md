|Type|Name|Notes|
|-----|-------|------|
|ObjectId|\_id|MongoDb's BSON ID|
|string|ArtistId|The string hex representation of the artist's ID|
|string|ArtistPfp|The path to the artist pfp (Currently Unimplemented)|
|string|ArtistName|The name of the artist|
|string|Bio|Info about the artist (Unimplemented)|
|long|PlayCount|The amount of times a track by this artist has been played|
|float|Rating|The rating of the album (Unimplemented)|
|List\<string\>|ArtistArtPaths|Fullscreen artwork paths(Unimplemented)|
|List\<string\>|ArtistBannerPaths|Banner sized artwork paths(Unimplemented)|
|List\<string\>|Genres|Genres found from tracks this artist has made|
|List\<[[ShortAlbum]]\>|Releases|Info on releases this artist put out|
|List\<[[ShortAlbum]]\>|SeenOn|Info on releases this artist was seen on(ex: compilation albums)|
|List\<[[ShortTrack]]\>|Tracks|Info on the artist's tracks|

## Example
```
{
  "_id": {
    "timestamp": 1703326975,
    "machine": 7304505,
    "pid": -12135,
    "increment": 13407738,
    "creationTime": "2023-12-23T10:22:55Z"
  },
  "artistId": "6586b4ff6f7539d099cc95fa",
  "artistPfp": "",
  "artistName": "Carly Rae Jepsen",
  "bio": "",
  "playCount": 0,
  "rating": 0,
  "artistArtPaths": null,
  "artistBannerPaths": null,
  "genres": [
    "Pop",
    "",
    "Electronic",
    "Dance-pop",
    "Synth-pop",
    "Disco",
    "Boogie",
    "Indie Pop"
  ],
  "releases": [
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407751,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "albumId": "6586b4ff6f7539d099cc9607",
      "albumName": "Kiss",
      "releaseDate": "2012-01-01T00:00:00Z",
      "releaseType": ""
    },
    {
      "_id": {
        "timestamp": 1703326976,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407787,
        "creationTime": "2023-12-23T10:22:56Z"
      },
      "albumId": "6586b5006f7539d099cc962b",
      "albumName": "The Loveliest Time",
      "releaseDate": "2023-01-01T00:00:00Z",
      "releaseType": ""
    },
    {
      "_id": {
        "timestamp": 1703326977,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407813,
        "creationTime": "2023-12-23T10:22:57Z"
      },
      "albumId": "6586b5016f7539d099cc9645",
      "albumName": "Tug Of War",
      "releaseDate": "2008-01-01T06:00:00Z",
      "releaseType": ""
    },
    {
      "_id": {
        "timestamp": 1703326977,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407833,
        "creationTime": "2023-12-23T10:22:57Z"
      },
      "albumId": "6586b5016f7539d099cc9659",
      "albumName": "The Loneliest Time",
      "releaseDate": "2022-10-21T05:00:00Z",
      "releaseType": ""
    },
    {
      "_id": {
        "timestamp": 1703326978,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407866,
        "creationTime": "2023-12-23T10:22:58Z"
      },
      "albumId": "6586b5026f7539d099cc967a",
      "albumName": "Dedicated Side B",
      "releaseDate": "2020-05-21T05:00:00Z",
      "releaseType": ""
    },
    {
      "_id": {
        "timestamp": 1703326978,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407891,
        "creationTime": "2023-12-23T10:22:58Z"
      },
      "albumId": "6586b5026f7539d099cc9693",
      "albumName": "Dedicated",
      "releaseDate": "2019-05-17T05:00:00Z",
      "releaseType": ""
    },
    {
      "_id": {
        "timestamp": 1703326979,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407922,
        "creationTime": "2023-12-23T10:22:59Z"
      },
      "albumId": "6586b5036f7539d099cc96b2",
      "albumName": "Emotion (Deluxe)",
      "releaseDate": "2015-08-21T05:00:00Z",
      "releaseType": ""
    },
    {
      "_id": {
        "timestamp": 1703326979,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407952,
        "creationTime": "2023-12-23T10:22:59Z"
      },
      "albumId": "6586b5036f7539d099cc96d0",
      "albumName": "EMOTION SIDE B",
      "releaseDate": "2016-07-15T05:00:00Z",
      "releaseType": ""
    }
  ],
  "seenOn": [
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407739,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "albumId": "6586b4ff6f7539d099cc95fb",
      "albumName": "Curiosity",
      "releaseDate": "2012-01-01T00:00:00Z",
      "releaseType": ""
    }
  ],
  "tracks": [
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407740,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "trackId": "6586b4ff6f7539d099cc95fc",
      "album": {
        "_id": {
          "timestamp": 1703326975,
          "machine": 7304505,
          "pid": -12135,
          "increment": 13407739,
          "creationTime": "2023-12-23T10:22:55Z"
        },
        "albumId": "6586b4ff6f7539d099cc95fb",
        "albumName": "Curiosity",
        "releaseDate": "2012-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 1,
      "disc": 1,
      "trackName": "Call Me Maybe",
      "duration": "193986.66666666666",
      "trackArtCount": 1,
      "path": "Music\\Soundbites\\Carly Rae Jepsen\\Curiosity (2012)\\1 Carly Rae Jepsen - Call Me Maybe.flac",
      "trackArtists": [
        {
          "_id": {
            "timestamp": 1703326975,
            "machine": 7304505,
            "pid": -12135,
            "increment": 13407738,
            "creationTime": "2023-12-23T10:22:55Z"
          },
          "artistId": "6586b4ff6f7539d099cc95fa",
          "artistName": "Carly Rae Jepsen"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407742,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "trackId": "6586b4ff6f7539d099cc95fe",
      "album": {
        "_id": {
          "timestamp": 1703326975,
          "machine": 7304505,
          "pid": -12135,
          "increment": 13407739,
          "creationTime": "2023-12-23T10:22:55Z"
        },
        "albumId": "6586b4ff6f7539d099cc95fb",
        "albumName": "Curiosity",
        "releaseDate": "2012-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 2,
      "disc": 1,
      "trackName": "Curiosity",
      "duration": "206466.66666666666",
      "trackArtCount": 1,
      "path": "Music\\Soundbites\\Carly Rae Jepsen\\Curiosity (2012)\\2 Carly Rae Jepsen - Curiosity.flac",
      "trackArtists": [
        {
          "_id": {
            "timestamp": 1703326975,
            "machine": 7304505,
            "pid": -12135,
            "increment": 13407738,
            "creationTime": "2023-12-23T10:22:55Z"
          },
          "artistId": "6586b4ff6f7539d099cc95fa",
          "artistName": "Carly Rae Jepsen"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407744,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "trackId": "6586b4ff6f7539d099cc9600",
      "album": {
        "_id": {
          "timestamp": 1703326975,
          "machine": 7304505,
          "pid": -12135,
          "increment": 13407739,
          "creationTime": "2023-12-23T10:22:55Z"
        },
        "albumId": "6586b4ff6f7539d099cc95fb",
        "albumName": "Curiosity",
        "releaseDate": "2012-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 3,
      "disc": 1,
      "trackName": "Picture",
      "duration": "183573.33333333334",
      "trackArtCount": 1,
      "path": "Music\\Soundbites\\Carly Rae Jepsen\\Curiosity (2012)\\3 Carly Rae Jepsen - Picture.flac",
      "trackArtists": [
        {
          "_id": {
            "timestamp": 1703326975,
            "machine": 7304505,
            "pid": -12135,
            "increment": 13407738,
            "creationTime": "2023-12-23T10:22:55Z"
          },
          "artistId": "6586b4ff6f7539d099cc95fa",
          "artistName": "Carly Rae Jepsen"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407746,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "trackId": "6586b4ff6f7539d099cc9602",
      "album": {
        "_id": {
          "timestamp": 1703326975,
          "machine": 7304505,
          "pid": -12135,
          "increment": 13407739,
          "creationTime": "2023-12-23T10:22:55Z"
        },
        "albumId": "6586b4ff6f7539d099cc95fb",
        "albumName": "Curiosity",
        "releaseDate": "2012-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 4,
      "disc": 1,
      "trackName": "Talk To Me",
      "duration": "171613.33333333334",
      "trackArtCount": 1,
      "path": "Music\\Soundbites\\Carly Rae Jepsen\\Curiosity (2012)\\4 Carly Rae Jepsen - Talk To Me.flac",
      "trackArtists": [
        {
          "_id": {
            "timestamp": 1703326975,
            "machine": 7304505,
            "pid": -12135,
            "increment": 13407738,
            "creationTime": "2023-12-23T10:22:55Z"
          },
          "artistId": "6586b4ff6f7539d099cc95fa",
          "artistName": "Carly Rae Jepsen"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407748,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "trackId": "6586b4ff6f7539d099cc9604",
      "album": {
        "_id": {
          "timestamp": 1703326975,
          "machine": 7304505,
          "pid": -12135,
          "increment": 13407739,
          "creationTime": "2023-12-23T10:22:55Z"
        },
        "albumId": "6586b4ff6f7539d099cc95fb",
        "albumName": "Curiosity",
        "releaseDate": "2012-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 5,
      "disc": 1,
      "trackName": "Just A Step Away",
      "duration": "226960",
      "trackArtCount": 1,
      "path": "Music\\Soundbites\\Carly Rae Jepsen\\Curiosity (2012)\\5 Carly Rae Jepsen - Just A Step Away.flac",
      "trackArtists": [
        {
          "_id": {
            "timestamp": 1703326975,
            "machine": 7304505,
            "pid": -12135,
            "increment": 13407738,
            "creationTime": "2023-12-23T10:22:55Z"
          },
          "artistId": "6586b4ff6f7539d099cc95fa",
          "artistName": "Carly Rae Jepsen"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407750,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "trackId": "6586b4ff6f7539d099cc9606",
      "album": {
        "_id": {
          "timestamp": 1703326975,
          "machine": 7304505,
          "pid": -12135,
          "increment": 13407739,
          "creationTime": "2023-12-23T10:22:55Z"
        },
        "albumId": "6586b4ff6f7539d099cc95fb",
        "albumName": "Curiosity",
        "releaseDate": "2012-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 6,
      "disc": 1,
      "trackName": "Both Sides Now",
      "duration": "232280",
      "trackArtCount": 1,
      "path": "Music\\Soundbites\\Carly Rae Jepsen\\Curiosity (2012)\\6 Carly Rae Jepsen - Both Sides Now.flac",
      "trackArtists": [
        {
          "_id": {
            "timestamp": 1703326975,
            "machine": 7304505,
            "pid": -12135,
            "increment": 13407738,
            "creationTime": "2023-12-23T10:22:55Z"
          },
          "artistId": "6586b4ff6f7539d099cc95fa",
          "artistName": "Carly Rae Jepsen"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703326975,
        "machine": 7304505,
        "pid": -12135,
        "increment": 13407752,
        "creationTime": "2023-12-23T10:22:55Z"
      },
      "trackId": "6586b4ff6f7539d099cc9608",
      "album": {
        "_id": {
          "timestamp": 1703326975,
          "machine": 7304505,
          "pid": -12135,
          "increment": 13407751,
          "creationTime": "2023-12-23T10:22:55Z"
        },
        "albumId": "6586b4ff6f7539d099cc9607",
        "albumName": "Kiss",
        "releaseDate": "2012-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 1,
      "disc": 1,
      "trackName": "Tiny Little Bows",
      "duration": "201853.33333333334",
      "trackArtCount": 1,
      "path": "Music\\Soundbites\\Carly Rae Jepsen\\Kiss\\1-01 Tiny Little Bows.flac",
      "trackArtists": [
        {
          "_id": {
            "timestamp": 1703326975,
            "machine": 7304505,
            "pid": -12135,
            "increment": 13407738,
            "creationTime": "2023-12-23T10:22:55Z"
          },
          "artistId": "6586b4ff6f7539d099cc95fa",
          "artistName": "Carly Rae Jepsen"
        }
      ]
    }
  ]
}
```