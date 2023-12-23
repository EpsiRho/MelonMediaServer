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

The info class of a playlist. Does not contain it's tracks, get those from [[../queues/get-tracks]].

## Example
```
{
  "_id": {
    "timestamp": 1703329760,
    "machine": 7148649,
    "pid": -15018,
    "increment": 5430586,
    "creationTime": "2023-12-23T11:09:20Z"
  },
  "queueId": "6586bfe06d1469c55652dd3a",
  "curPosition": 0,
  "name": "newQueue",
  "owner": "Epsi",
  "editors": [],
  "viewers": [],
  "publicViewing": false,
  "publicEditing": false
}
```
