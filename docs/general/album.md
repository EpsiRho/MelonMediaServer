### Get
##### Request

```
GET /api/album[?id]
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
PATCH /api/album
```

##### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|body|[[../models/Album|Album]]||A track object that will override one with the same ID |

##### Responses
Returns 200 on success, 404 on track not found.