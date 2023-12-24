# albums
## Get Request

`GET /api/search/albums`

#### Parameters

|Name|Type|Default|Notes|
|---|---|---|---|
|page|int||The index of the page of results|
|count|int||The number of results in a returned page|
|albumName|string|""|The search query for name of album|
|publisher|string|""|Search albums by publisher|
|releaseType|string|""|Filter albums by releaseType, as classified [by MusicBrainz](https://musicbrainz.org/doc/Release_Group/Type)|
|releaseStatus|string|""|Filter albums by releases status (official, pseudo-release, promotion, etc)|
|ltPlayCount|int|0|Return albums played fewer times than value|
|gtPlayCount|int|0|Return albums played more times than value|
|ltYear|int|0|Return albums released before a certain year|
|gtYear|int|0|Return albums released after a certain year|
|ltMonth|int|0|Return albums released before a certain month|
|gtMonth|int|0|Return albums released after a certain month|
|ltDay|int|0|Return albums released before a certain day of month|
|gtDay|int|0|Return albums released after a certain day of month|
|ltRating|int|0|Return albums rated lower than value|
|gtRating|int|0|Return albums rated higher than value|
|genres|string[]||Return albums in any of the requested genres|

#### Responses
Returns List\<Albums\> of queried albums