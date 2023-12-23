##### Request
```
GET /auth/login[?username&password]
```

Authenticates a user for 60 minutes with a [JSON Web Token](https://jwt.io/)
##### Parameters
|Name|Type|Default|Notes|
|---|---|---|---|
|username|string|||
|password|string|||

##### Responses
Returns a string containing the JSON Web Token