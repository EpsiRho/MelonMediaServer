# artists
## Get Request

`GET /api/search/artists`

## Auth
Accepts Users of type Admin, User, Pass.</br>

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|page|int||The index of the page of results|
|count|int||The number of results in a returned page|
|artistName|string|""|The search query for name of artist|
|ltPlayCount|int|0|Return artists played fewer times than value|
|gtPlayCount|int|0|Return artists played more times than value|
|ltRating|int|0|Return artists rated lower than value|
|gtRating|int|0|Return artists rated higher than value|
|genres|string[]||Return artists in any of the requested genres|

## Responses
Returns a list of [Artist](models/Artist)