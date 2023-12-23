|Type|Name|Notes|
|----|----|----|
|ObjectId|\_id|MongoDb's BSON ID|
|string|UserId|The hex string representation of the user's ID|
|string|Username|The username of the user|
|string|Password|The hash of the password of the user|
|string|Bio|The user's bio|
|byte[]|Salt|The salt used to hash the password|
|string|Type|The type of user (Admin, User, Pass)|
|DateTime|LastLogin|The last login date and time|
