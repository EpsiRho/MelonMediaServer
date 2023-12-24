# search
## Get Request

`GET /api/queus/search[?page&count&name]`

## Auth
Accepts Users of type Admin, User, or Pass.</br>
Authenticated user must be queue owner.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|page|int||Used for pagination, the page you are on|
|count|int||Used for pagination, how many queues per page|
|name|string|""|The search query for the name of the queue|

## Responses
200 - Returns a list of [PlayQueue](models/PlayQueue)