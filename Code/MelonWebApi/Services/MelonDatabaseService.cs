using Melon.LocalClasses;
using Melon.Models;
using MongoDB.Driver;

namespace MelonWebApi.Services
{
    public class MelonDatabaseService
    {
        public MelonDatabaseService() 
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var _TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
        }

    }
}
