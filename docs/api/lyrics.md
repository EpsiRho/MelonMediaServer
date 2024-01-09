# lyrics
## Get Request

`GET /api/lyrics[?id]`

## Auth
Accepts Users of type Admin or User.</br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The ID of the track to get lyrics from|

## Responses
- 200 the .lrc file text
- 404 Track Not Found / Track Does Not Have Lyrics