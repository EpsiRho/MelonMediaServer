# delete
Delete a user by their id
## Get Request

`GET /api/users/delete[?id]`

## Auth
Accepts Users of type Admin. </br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the user to delete|

## Responses
Returns 
- 200 User deleted
- 403 Invalid Auth
- 404 User Not Found