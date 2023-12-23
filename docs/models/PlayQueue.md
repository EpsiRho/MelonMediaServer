|Type|Name|Notes|
|----|----|-----|
|ObjectId|\_id|MongoDb's BSOn ID|
|string|QueueId|The hex string representation of the playlist's ID|
|string|Name|The name of the playlist|
|int|CurPosition|The current position in the queue|
|string|Owner|The username of the owner|
|List\<string\>|Editors|Usernames of users allowed to edit this queue|
|List\<string\>|Viewers|Usernames of uses allowed to view and listen to this queue|
|bool|PublicViewing|Allow anyone to see this queue (false by default)|
|bool|PublicEditing|Allow anyone to edit this queue (false by default)|
|List\<[[ShortTrack]]\>|Tracks|The tracks in this queue|

**This model will not be sent back to you!** Normally you'll be sent a [[ShortQueue]]. Since queues can hold a lot of tracks, you'll get those tracks through a different endpoint than you use to get the playlist info. 
- Viewing queue info
	- [[../queues/get]]
	- [[../queues/search]]
- Viewing queue tracks
	- [[../queues/get-tracks]]
