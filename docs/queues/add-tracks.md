# add-tracks

## Post Request

`POST /api/queues/add-tracks[?id]`

## Auth
Accepts Users of type Admin or User.</br>
Authenticated user must be queue owner or editor.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the queue to add tracks to|
|request body|array||A json array of track ids to add to the queue|

## Responses
Returns 
- 200 Tracks added
- 401 Invalid Auth
- 404 Queue not found