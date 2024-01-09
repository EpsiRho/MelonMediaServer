# current
Get the current authenticated user.
## Get Request

`GET /api/users/current`

## Auth
Accepts Users of type Admin, User or Pass.</br>

## Responses
Returns 
- 200 returns a [PublicUser](models/PublicUser)
- 403 Invalid Auth