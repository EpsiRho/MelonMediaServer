## api/track

- [x] (http)get - Get a single track by it's ID
- [x] (http)patch - Update a track's info

## api/album

- [x] (http)get - Get a single album by it's ID
- [x] (http)patch - Update an album's info

## api/artist

- [x] (http)get - Get a single artist by it's ID
- [x] (http)patch - Update an artist's info

## api/stats/

- [x] log-play - Logs a play into the play count of a track, it's album, and it's artist
- [ ] top-tracks - Get the top tracks
- [ ] top-albums - Get the top albums
- [ ] top-artists - Get the top artists

## api/recommendations/

// ToDo

## api/search/

- [x] tracks - Search for tracks with parameters
- [x] albums - Search for albums with parameters
- [x] artists - Search for artists with parameters

## api/db/

- [ ] formats - Returns a list of all formats in the db
- [ ] bitrates - Returns a list of all bitrates in the db
- [ ] sample-rates - Returns a list of all sample rates in the db
- [ ] channels - Returns a list of all channel setups in the db
- [ ] bits-per-sample - Returns a list of all bits per sample types in the db
- [ ] genres - Returns a list of all genres in the db

- [ ] 

## api/download/

- [x] track - Get a track's file
- [x] track-art - Get a track's art
- [x] album-art - Get an album's art

## api/playlists/

- [x] create - Create a new playlist
- [x] add-tracks - Add tracks to a playlists
- [x] remove-tracks - Remove tracks from a playlist
- [x] update - Update a playlist's info and tracks
- [ ] move-track - Move a track from x to y position in a playlist
- [ ] search - Get playlists 
  - [ ] Page and Count parameters
  - [ ] ShortPlaylist
  - [ ] By name
  - [ ] Sorting
- [x] get - Get a full playlist by ID

## api/queues/

- [x] create - Create a queue with a list of IDs
- [ ] add-tracks - Add tracks to a queue
- [ ] remove-tracks - Remove tracks from a queue
- [ ] move-track - Move a track from x to y position in a queue
- [ ] update - Update a queue's info and tracks
- [ ] position - Change the currentPosition in the queue
- [ ] create-from-albums - Create a queue from album IDs
- [ ] create-from-artists - create a queue from artist IDs
- [ ] shuffle - shuffle a queue with a specific shuffle type
  - [ ] Random shuffle
  - [ ] Prioritize high play count and rating
  - [ ] Prioritize low play count and unrated
  - [ ] Shuffle by album
- [x] get - Get a single queue
- [ ] search - Get all queues

## api/auth/

- [ ] login - Log the user in and get an api key



