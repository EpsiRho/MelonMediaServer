# update
This will take a playlist object and use it to replace the properties of the playlist with the same ID.
## Post Request

`POST /api/playlists/update`

## Auth
Accepts Users of type Admin or User.</br>
Authenticated user must be playlist owner or editor, or the playlist must allow public editing.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|request body|[ShortPlaylist](models/ShortPlaylist)||A ShortPlaylist object in json format|

## Responses
Returns
Returns 
- 200 Playlist updated
- 401 Invalid Auth
- 404 Playlist not found