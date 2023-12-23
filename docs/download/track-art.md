# track-art
## Get Request

`GET /api/download/track-art[?id&index]`

#### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the track|
|index|int||The index of the art to get(if the track has multiple, starts at 0)|

#### Responses
Returns the track art at the index specified.