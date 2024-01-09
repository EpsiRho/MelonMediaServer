# update
Create a new user
## Get Request

`PATCH /api/users/update[?id&bio&role&publicStats&favTrackId&favAlbumId&favArtistId]`

## Auth
Accepts Users of type Admin, User, Pass. </br>
Only Admins can change role

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||The id of the user to update|
|bio|string||The user's bio|
|role|string||The role, currently supports Admin, User, or Pass|
|publicStats|bool||If the user's stats/profile should be public|
|favTrackId|string||A track ID for the user's favorite track|
|favAlbumId|string||An album ID for the user's favorite album|
|favArtistId|string||An artist ID for the user's favorite artist|

#### Notes
- Roles
  - **Admin** roles can access any endpoint
  - **User** roles can only access read endpoints, and write endpoints for their allowed objects, such as playlists or queues.
  - **Pass** roles can only read, and they cannot play tracks.

## Responses
Returns 
- 200 User Updated
- 401 Invalid Auth
- 404 User Not Found