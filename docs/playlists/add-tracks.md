# add-tracks

## Post Request

`POST /api/playlists/add-tracks[?id]`

## Auth
Accepts Users of type Admin or User.</br>
Authenticated user must be playlist owner or editor, or the playlist must allow public editing.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the playlist to add tracks to|
|request body|array||A json array of track ids to add to the playlist|

## Responses
Returns 
- 200 Tracks added
- 401 Invalid Auth
- 404 Playlist not found