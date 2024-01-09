# search
Search for users by their username
## Get Request

`GET /api/users/search[?username]`

## Auth
Accepts Users of type Admin. </br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|username|string||The query for the username parameter|

## Responses
Returns 
- 200 returns a list of matching [PublicUser](models/PublicUser)
- 401/403 Invalid Auth