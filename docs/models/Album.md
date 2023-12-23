# Album

## Model
|Type|Name|Notes|
|-----|-------|------|
|ObjectId| \_id |MongoDb's BSON ID| 
|string| AlbumId |The Album's ID in a hex string representation|
|int| TotalDiscs|The total number of discs|
|int| TotalTracks|The total number of tracks|
|string| AlbumName|The name of the album|
|string| Bio|Information about the album(currently Unused)|
|string| Publisher|The album's publisher|
|string| ReleaseStatus|Release status (Official, Bootleg, Promotion)|
|string| ReleaseType|The album's type (Album, Single, EP, Broadcast, Other)|
|long| PlayCount|The amount of times a track from this album has been played|
|float| Rating| The album's rating (no official rating system has been decided)|
|DateTime| ReleaseDate|The release date of the album|
|List\<string\>| AlbumArtPaths|Names of the art on disk|
|List\<string\>| AlbumGenres|Genres found on tracks on the album|
|List\<[ShortArtist](models/ShortArtist)\>| AlbumArtists|Artists found on tracks on the album|
|List\<[ShortTrack](models/ShortTrack)\>| Tracks|All track in the album|
## Example
```
{
  "_id": {
    "timestamp": 1703296729,
    "machine": 5705263,
    "pid": 287,
    "increment": 2175663,
    "creationTime": "2023-12-23T01:58:49Z"
  },
  "albumId": "65863ed9570e2f011f2132af",
  "totalDiscs": 0,
  "totalTracks": 0,
  "albumName": "Adventure",
  "bio": "",
  "publisher": "",
  "releaseStatus": "official",
  "releaseType": "album",
  "playCount": 0,
  "rating": 0,
  "releaseDate": "2022-03-30T05:00:00Z",
  "albumArtPaths": [
    "Melon/AlbumArts/65863ed9570e2f011f2132af-0.jpg"
  ],
  "albumGenres": [
    "Electronic",
    "Pop",
    "Dance-pop",
    "Electro House",
    "Synth-pop"
  ],
  "albumArtists": [
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175665,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "artistId": "65863ed9570e2f011f2132b1",
      "artistName": "Kyan"
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175670,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "artistId": "65863ed9570e2f011f2132b6",
      "artistName": "Dan Smith"
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175673,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "artistId": "65863ed9570e2f011f2132b9",
      "artistName": "Passion Pit"
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175682,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "artistId": "65863ed9570e2f011f2132c2",
      "artistName": "Mark Foster"
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175685,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "artistId": "65863ed9570e2f011f2132c5",
      "artistName": "Aquilo"
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175694,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "artistId": "65863ed9570e2f011f2132ce",
      "artistName": "Nicholas Petricca"
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175703,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "artistId": "65863ed9570e2f011f2132d7",
      "artistName": "Vancouver Sleep Clinic"
    }
  ],
  "tracks": [
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175667,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132b3",
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
      "position": 2,
      "disc": 1,
      "trackName": "You're On",
      "duration": "193",
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
        },
        {
          "_id": {
            "timestamp": 1703296729,
            "machine": 5705263,
            "pid": 287,
            "increment": 2175665,
            "creationTime": "2023-12-23T01:58:49Z"
          },
          "artistId": "65863ed9570e2f011f2132b1",
          "artistName": "Kyan"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175669,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132b5",
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
      "position": 3,
      "disc": 1,
      "trackName": "OK",
      "duration": "182",
      "trackArtCount": 1,
      "path": "CMusic\\Soundbites\\Madeon\\Adventure (Deluxe)",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175672,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132b8",
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
      "position": 4,
      "disc": 1,
      "trackName": "La Lune",
      "duration": "220",
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
        },
        {
          "_id": {
            "timestamp": 1703296729,
            "machine": 5705263,
            "pid": 287,
            "increment": 2175670,
            "creationTime": "2023-12-23T01:58:49Z"
          },
          "artistId": "65863ed9570e2f011f2132b6",
          "artistName": "Dan Smith"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175675,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132bb",
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
      "position": 5,
      "disc": 1,
      "trackName": "Pay No Mind",
      "duration": "249",
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
        },
        {
          "_id": {
            "timestamp": 1703296729,
            "machine": 5705263,
            "pid": 287,
            "increment": 2175673,
            "creationTime": "2023-12-23T01:58:49Z"
          },
          "artistId": "65863ed9570e2f011f2132b9",
          "artistName": "Passion Pit"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175677,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132bd",
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
      "position": 6,
      "disc": 1,
      "trackName": "Beings",
      "duration": "215",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175679,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132bf",
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
      "position": 7,
      "disc": 1,
      "trackName": "Imperium",
      "duration": "199",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175681,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132c1",
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
      "position": 8,
      "disc": 1,
      "trackName": "Zephyr",
      "duration": "220",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175684,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132c4",
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
      "position": 9,
      "disc": 1,
      "trackName": "Nonsense",
      "duration": "226",
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
        },
        {
          "_id": {
            "timestamp": 1703296729,
            "machine": 5705263,
            "pid": 287,
            "increment": 2175682,
            "creationTime": "2023-12-23T01:58:49Z"
          },
          "artistId": "65863ed9570e2f011f2132c2",
          "artistName": "Mark Foster"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175687,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132c7",
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
      "position": 10,
      "disc": 1,
      "trackName": "Innocence",
      "duration": "224",
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
        },
        {
          "_id": {
            "timestamp": 1703296729,
            "machine": 5705263,
            "pid": 287,
            "increment": 2175685,
            "creationTime": "2023-12-23T01:58:49Z"
          },
          "artistId": "65863ed9570e2f011f2132c5",
          "artistName": "Aquilo"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175689,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132c9",
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
      "position": 11,
      "disc": 1,
      "trackName": "Pixel Empire",
      "duration": "245",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175691,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132cb",
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
      "position": 12,
      "disc": 1,
      "trackName": "Home",
      "duration": "225",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175693,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132cd",
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
      "disc": 2,
      "trackName": "Icarus",
      "duration": "215",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175696,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132d0",
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
      "position": 2,
      "disc": 2,
      "trackName": "Finale",
      "duration": "205",
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
        },
        {
          "_id": {
            "timestamp": 1703296729,
            "machine": 5705263,
            "pid": 287,
            "increment": 2175694,
            "creationTime": "2023-12-23T01:58:49Z"
          },
          "artistId": "65863ed9570e2f011f2132ce",
          "artistName": "Nicholas Petricca"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175698,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132d2",
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
      "position": 3,
      "disc": 2,
      "trackName": "The City",
      "duration": "234",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175700,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132d4",
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
      "position": 4,
      "disc": 2,
      "trackName": "Cut the Kid",
      "duration": "198",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175702,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132d6",
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
      "position": 5,
      "disc": 2,
      "trackName": "Technicolor",
      "duration": "385",
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
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175705,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132d9",
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
      "position": 6,
      "disc": 2,
      "trackName": "Only Way Out",
      "duration": "227",
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
        },
        {
          "_id": {
            "timestamp": 1703296729,
            "machine": 5705263,
            "pid": 287,
            "increment": 2175703,
            "creationTime": "2023-12-23T01:58:49Z"
          },
          "artistId": "65863ed9570e2f011f2132d7",
          "artistName": "Vancouver Sleep Clinic"
        }
      ]
    },
    {
      "_id": {
        "timestamp": 1703296729,
        "machine": 5705263,
        "pid": 287,
        "increment": 2175707,
        "creationTime": "2023-12-23T01:58:49Z"
      },
      "trackId": "65863ed9570e2f011f2132db",
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
        "releaseDate": "0001-01-01T00:00:00Z",
        "releaseType": ""
      },
      "position": 7,
      "disc": 2,
      "trackName": "Together",
      "duration": "148",
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
  ]
}
```