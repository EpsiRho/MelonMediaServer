# User
This is the user model for melon, more elements may be added later. Currently, there is no way to access these through endpoints, you can add or remove users through the console UI.
# Model
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
