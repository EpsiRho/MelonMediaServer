# log-play

## POST request
`/api/stats/log-play[?id&device&dateTime]`

## Auth
Accepts Users of type Admin, User.

## Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|id|string||ID of logged track|
|device|string|""|ID of device logging track|
|dateTime|string|""|Date of log|

## Responses
- 404 Track not found
- 400 Invalid Datetime
- 200 Play logged