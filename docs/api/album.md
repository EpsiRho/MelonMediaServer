# album

## Get Request

`GET /api/album[?id]`

#### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The ID of the album to get|

#### Responses
Returns an [Album](/models/Album) object with info about the album.

---

## PATCH Request 
**(Unfinished, will not update whole database)**

`PATCH /api/album`

#### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|request body|[Album](/models/Album)||An Album object that will override one with the same ID |

##### Responses
Returns 200 on success, 404 on track not found.