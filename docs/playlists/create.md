# create

## Post Request

`POST /api/playlists/create[?name&description&artworkPath]`

## Auth
Accepts Users of type Admin or User.</br>
Authenticated user will be set as the owner of the new playlist.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|name|string||The name of the new playlist|
|description|string|""|The description of the new playlist|
|request body|array||A json array of track IDs to add to the new playlist|

## Responses
Returns the hex string ID of the newly created playlist.