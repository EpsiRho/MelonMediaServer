|Type|Name|Notes|
|----|------|-----|
|ObjectId|\_id|MongoDb's BSON ID|
|string|StatId|The hex string representation of the stat's ID|
|string|TrackId|The hex string representation of the ID of the track that was logged|
|string|AlbumId|The hex string representation of the ID of the album that was logged|
|List\<string\>|ArtistIds|The hex string representation of the ID of the artists that were logged|
|List\<string\>|Genres|The genres from the track that was logged|
|string|Device|The device the log is from (if set)|
|string|User|The user the log is for|
|DateTime|LogDate|The date and time of the log|
