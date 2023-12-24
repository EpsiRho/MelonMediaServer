# get

## Get Request

`GET /api/queues/get[?id]`

## Auth
Accepts Users of type Admin or User.
Authenticated user must be queue owner.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||ID of requested queue|

## Responses
Returns
- 200 with a [Queue](models/queue) object as json
- 401 Invalid auth
- 404 Playlist not found
