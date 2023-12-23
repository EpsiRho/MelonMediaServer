# tracks
## Get Request

`GET /api/search/tracks`

#### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|page|int||The index of the page of results|
|count|int||The number of results in a returned page|
|trackName|string|""|The search query for name of track|
|format|string|""|The queried audio format|
|bitrate|string|""|The queried audio bitrate|
|sampleRate|string|""|Query audio sample rate|
|channels|string|""|Query for audio channels|
|bitsPerSample|string|""|Audio bits per sample|
|year|string|""|Tracks year of release|
|ltPlayCount|int|0|Return tracks played fewer times than value|
|gtPlayCount|int|0|Return tracks played more times than value|
|ltSkipCount|int|0|Return tracks skipped fewer times than value|
|gtSkipCount|int|0|Return tracks skipped more times than value|
|ltYear|int|0|Return tracks released before a certain year|
|gtYear|int|0|Return tracks released after a certain year|
|ltMonth|int|0|Return tracks released before a certain month|
|gtMonth|int|0|Return tracks released after a certain month|
|ltDay|int|0|Return tracks released before a certain day of month|
|gtDay|int|0|Return tracks released after a certain day of month|
|ltRating|int|0|Return tracks rated lower than value|
|gtRating|int|0|Return tracks rated higher than value|
|genres|string[]|||

#### Responses
Returns List<Track> of queried tracks