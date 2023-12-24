# move-track
Moves a track to a new position
## Post Request

`POST /api/queues/move-track[?queueId&trackId&position]`

## Auth
Accepts Users of type Admin or User.</br>
Authenticated user must be queue owner.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|playlistId|string||The id of the queue to edit|
|trackId|string||The id of the track to move|
|position|int||The position to move the track to|

## Responses

Returns 
- 200 Track Moved
- 401 Invalid Auth
- 404 Queue not found