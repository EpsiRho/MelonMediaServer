# track
## Get Request

`GET /api/track[?id]`

## Auth
Accepts Users of type Admin, User, Pass.</br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The ID of the track to get|

## Responses
Returns a [Track](models/track) object with info about the track.

---

## PATCH Request
**(Unfinished, will not update whole database)**

`PATCH /api/track`

## Auth
Accepts Users of type Admin.</br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|request body|[Track](models/track)||A track object that will override one with the same ID |

## Responses
Returns 200 on success, 404 on track not found.
