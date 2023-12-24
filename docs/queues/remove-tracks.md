# remove-tracks

## Post Request

`POST /api/queues/remove-tracks[?id]`

## Auth
Accepts Users of type Admin or User.</br>
Authenticated user must be queue owner or editor, or the queue must allow public editing.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the queue to remove tracks from|
|request body|array||A json array of track ids to remove from the queue|

## Responses
Returns 
- 200 Tracks removed
- 401 Invalid Auth
- 404 Queue not found