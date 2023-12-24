## Get Request

`GET /api/playlists/get-tracks[?page&count&id]`

## Auth
Accepts Users of type Admin, User, or Pass.</br>
Authenticated user must be playlist owner or viewer, or the playlist must allow public viewing.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|page|int||Used for pagination, the page you are on|
|count|int||Used for pagination, how many tracks per page|
|id|string||The ID of the playlist to get tracks from|

## Responses
Returns 
- 200 with a list of [ShortTrack](models/ShortTrack) as json
- 401 Invalid Auth
- 404 Playlist not found