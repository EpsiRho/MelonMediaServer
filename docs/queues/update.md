# update
This will take a queue object and use it to replace the properties of the queue with the same ID.
## Post Request

`POST /api/queues/update`

## Auth
Accepts Users of type Admin or User.</br>
Authenticated user must be queue owner.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|request body|[ShortQueue](models/ShortQueue)||A ShortQueue object in json format|

## Responses
Returns
Returns 
- 200 Queue updated
- 401 Invalid Auth
- 404 queue not found