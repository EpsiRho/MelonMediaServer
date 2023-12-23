##### Request

```
GET /api/download/album-art[?id&index]
```

##### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the album|
|index|int||The index of the art to get(if the album has multiple)|

##### Responses
Returns the track art at the index specified.