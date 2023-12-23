### Get
##### Request

```
GET /api/track[?id]
```

##### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The ID of the track to get|

##### Responses
Returns a [[../models/Track]] object with info about the track.

### PATCH (Unfinished, will not update whole database)
##### Request

```
PATCH /api/track
```

##### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|body|[[models/Track\|Track]]||A track object that will override one with the same ID |

##### Responses
Returns 200 on success, 404 on track not found.
