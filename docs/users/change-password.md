# change-password
Create a new user
## Get Request

`PATCH /api/users/change-password[?id&password]`

## Auth
Accepts Users of type Admin, User, Pass. </br>
Admins can change any user's password.</br>
Changing password will invalidate any current auth tokens for that user.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the user to update|
|password|string||The new password|

## Responses
Returns 
- 200 Password Changed
- 403 Invalid Auth
- 404 User Not Found