# create

## Post Request

`GET /api/queues/create[?name&_ids&shuffle]`

## Auth
Accepts Users of type Admin or User.
Authenticated user will be set as the owner of the queue.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|name|string||The name of the queue|
|request body|array||A json array of track ids to add to the queue|
|boolean|shuffle|none|Shuffle mode of the created queue|