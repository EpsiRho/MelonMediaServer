# Melon Media Server

## About
This is an ambitious project I started working on because I didn't like other solutions. I hope to include lots of customization and functionality to make this the best way to view, manage, and listen to your local music library.

## Setting up a melon server
Right now, you'll have to clone the repository and build the server yourself. This will change later in development, and better guides will come when it does. 

You'll need a [MongoDB](https://www.mongodb.com/try/download/community) instance to connect the server to, so spin one up either through atlas or locally before running the server.

When you launch the server, launch using the **MelonWebApi** project. If it is your first time, it will take you through the initial setup process. Then you'll want to run your first scan of your files once you've dropped into the main menu.

## Where to start building apps
All endpoints require authentication except for logging in, so [auth](auth/authOverview) is a good place to start. </br>
Once you've obtained a JWT, a good way to test the api is with [search](search/searchOverview) endpoints