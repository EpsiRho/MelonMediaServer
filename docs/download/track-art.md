# track-art
## Get Request

`GET /api/download/track-art[?id&index]`

## Auth
Accepts Users of type Admin, User, Pass.</br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the track|
|index|int||The index of the art to get(if the track has multiple, starts at 0)|

## Responses
Returns the track art at the index specified.