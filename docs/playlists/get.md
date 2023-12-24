# get
Get the information of a playlist
## Get Request

`GET /api/playlists/get[?id]`

## Auth
Accepts Users of type Admin, User, or Pass.</br>
Authenticated user must be playlist owner or viewer, or the playlist must allow public viewing.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the playlist to get|

## Responses
Returns 
- 200 with a [ShortPlaylist](models/ShortPlaylist) as json
- 401 Invalid Auth
- 404 Playlist not found