# top-artists

## GET request

`/api/stats/top-artists[?user]`

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|user|string||Username|
|ltDateTime|string||Include only logs before a certain date|
|gtDateTime|string||Include only logs after a certain date|
|page|int||Used for pagination, the page you are on|
|count|int||Used for pagination, how many artists per page|

## Responses

- 200 Returns list of key, value pairs with artist ids and number of plays
- 401 Invalid auth
- 404 User not found