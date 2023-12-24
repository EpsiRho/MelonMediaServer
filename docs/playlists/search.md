# search
## Get Request

`GET /api/playlists/search[?page&count&name]`

## Auth
Accepts Users of type Admin, User, or Pass.</br>
Authenticated user must be playlist owner or viewer, or the playlist must allow public viewing.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|page|int||Used for pagination, the page you are on|
|count|int||Used for pagination, how many playlists per page|
|name|string|""|The search query for the name of the playlist|

## Responses
200 - Returns a list of [ShortPlaylist](models/ShortPlaylist)