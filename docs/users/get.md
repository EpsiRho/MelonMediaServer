# get
Get a user by their id
## Get Request

`GET /api/users/get[?id]`

## Auth
Accepts Users of type Admin. </br>
Accepts User or Pass if the authenticated user id matches the id to get.</br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the user to get|

## Responses
Returns 
- 200 returns a [PublicUser](models/PublicUser)
- 401 Invalid Auth
- 404 User Not Found