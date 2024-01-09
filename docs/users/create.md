# create
Create a new user
## Get Request

`POST /api/users/create[?username&password&role]`

## Auth
Accepts Users of type Admin, Server. </br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|username|string||The username of the new user|
|password|string||The password for authentication|
|role|string||The role, currently supports Admin, User, or Pass|

#### Notes
- *username* must be unique!
- Roles
  - **Admin** roles can access any endpoint
  - **User** roles can only access read endpoints, and write endpoints for their allowed objects, such as playlists or queues.
  - **Pass** roles can only read, and they cannot play tracks.

## Responses
Returns 
- 200 returns a user id for the new user
- 403 Invalid Auth
- 409 Username is Taken