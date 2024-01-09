# change-username
Create a new user
## Get Request

`PATCH /api/users/change-username[?id&username]`

## Auth
Accepts Users of type Admin, User, Pass. </br>
Admins can change any user's name.</br>
Changing username will invalidate any current auth tokens for that user.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the user to update|
|username|string||The new username|

## Responses
Returns 
- 200 Username Changed
- 403 Invalid Auth
- 404 User Not Found