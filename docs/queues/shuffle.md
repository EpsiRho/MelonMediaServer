# shuffle

Shuffle queue
## Post Request

`POST /api/queues/shuffle[?id&shuffle]
## Auth
Accepts Users of type Admin or User.</br>
Authenticated user must be queue owner.
## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||ID of queue
|shuffle|string||The requested shuffle mode|

## Responses

Returns
- 200 Tracks Shuffled
- 401 Invalid Auth
- 404 Queue n

