# album-art
## Get Request

`GET /api/download/album-art[?id&index]`

#### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the album|
|index|int||The index of the art to get(if the album has multiple, starts at 0)|

#### Responses
Returns the track art at the index specified.