Authentication in Melon is done using [JSON Web Tokens](https://jwt.io/) tokens. You can create and manage users in the console UI, under `Settings->Users.`
You authenticate using the [[Login]] endpoint. This will give you a JWT that lasts 60 minutes. You'll place this in the authentication header for any other request to the server to authenticate your user and associated privileges.

A user can be one of 3 levels of privilege:
- Admin
	- Admins can execute any endpoint, including editing/deletion endpoints.
- User
	- Users can only interact with endpoints that list or play.
- Pass
	- Pass users can only see tracks and stats, along with your user profile. (Needs Implementing)

## All Endpoints
- [[Login]]