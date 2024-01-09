
Server A creates invite code.
Give invite code to Server B
App needs server url, invite code, username, password
app sends Server B's API api/users/create-connection
create-connection needs to talk to Sever A, api/user/create
Save userId and username, password, and url. all encrypt on disk.