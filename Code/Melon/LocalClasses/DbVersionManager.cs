using Melon.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Melon.LocalClasses.StateManager;

namespace Melon.LocalClasses
{
    public static class DbVersionManager
    {
        public const string ArtistsVersion = "1.0.0";
        public const string AlbumsVersion = "1.0.0";
        public const string TracksVersion = "1.1.0";
        public const string FailedFilesVersion = "1.0.0";
        public const string PlaylistsVersion = "1.0.0";
        public const string CollectionsVersion = "1.0.0";
        public const string QueuesVersion = "1.0.0";
        public const string UsersVersion = "1.0.0";
        public const string StatsVersion = "1.0.0";
        public static string CheckVersionCompatibility()
        {
            var newMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var metadataCollection = newMelonDB.GetCollection<DbMetadata>("Metadata");
            var artistMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "ArtistsCollection").FirstOrDefault();
            var albumMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "AlbumsCollection").FirstOrDefault();
            var trackMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "TracksCollection").FirstOrDefault();
            var failedFilesMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "FailedFilesCollection").FirstOrDefault();
            var playlistsMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "PlaylistsCollection").FirstOrDefault();
            var collectionsMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "CollectionsCollection").FirstOrDefault();
            var queuesMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "QueuesCollection").FirstOrDefault();
            var usersMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "UsersCollection").FirstOrDefault();
            var statsMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "StatsCollection").FirstOrDefault();

            List<string> incompatibleCollections = new List<string>();

            if (statsMetadata != null)
            {
                if (statsMetadata.Version != StatsVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported User Database Version");
                    incompatibleCollections.Add("Stats");
                }
            }
            else
            {
                var statMetadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "StatsCollection",
                    Version = StatsVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(statMetadata);
            }

            if (usersMetadata != null)
            {
                if (usersMetadata.Version != UsersVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported User Database Version");
                    incompatibleCollections.Add("Users");
                }
            }
            else
            {
                var userMetadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "UsersCollection",
                    Version = UsersVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(userMetadata);
            }

            if (artistMetadata != null)
            {
                if (artistMetadata.Version != ArtistsVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Artist Database Version");
                    incompatibleCollections.Add("Artists");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "ArtistsCollection",
                    Version = ArtistsVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (albumMetadata != null)
            {
                if (albumMetadata.Version != AlbumsVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Album Database Version");
                    incompatibleCollections.Add("Albums");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "AlbumsCollection",
                    Version = AlbumsVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (trackMetadata != null)
            {
                if (trackMetadata.Version != TracksVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Track Database Version");
                    incompatibleCollections.Add("Tracks");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "TracksCollection",
                    Version = TracksVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (failedFilesMetadata != null)
            {
                if (failedFilesMetadata.Version != FailedFilesVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported FailedFiles Database Version");
                    incompatibleCollections.Add("FailedFiles");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "FailedFilesCollection",
                    Version = FailedFilesVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (playlistsMetadata != null)
            {
                if (playlistsMetadata.Version != PlaylistsVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Playlists Database Version");
                    incompatibleCollections.Add("Playlists");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "PlaylistsCollection",
                    Version = PlaylistsVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (collectionsMetadata != null)
            {
                if (collectionsMetadata.Version != CollectionsVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Collections Database Version");
                    incompatibleCollections.Add("Collections");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "CollectionsCollection",
                    Version = CollectionsVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (queuesMetadata != null)
            {
                if (queuesMetadata.Version != QueuesVersion)
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Queues Database Version");
                    incompatibleCollections.Add("Queues");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "QueuesCollection",
                    Version = QueuesVersion,
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            return String.Join(",", incompatibleCollections);
        }
        public static void ConvertCollectionsUI(string[] collections)
        {
            Console.WriteLine($"[-] {StringsManager.GetString("BackingUpDatabase")}");
            var backup = Transfer.ExportDb();
            if (!backup)
            {
                Console.WriteLine($"[#] {StringsManager.GetString("DatabaseBackupFailed")}");
                Environment.Exit(2);
            }
            Console.WriteLine($"[+] {StringsManager.GetString("BackupCreated")}");

            foreach (var col in collections)
            {
                Console.WriteLine($"[-] {StringsManager.GetString("Converting")} {col}");

                switch (col)
                {
                    case "Artists":
                        ConvertDocuments<Artist>(col, ArtistsVersion);
                        break;
                    case "Albums":
                        ConvertDocuments<Album>(col, AlbumsVersion);
                        break;
                    case "Tracks":
                        ConvertDocuments<Track>(col, TracksVersion);
                        break;
                    case "FailedFiles":
                        ConvertDocuments<FailedFile>(col, FailedFilesVersion);
                        break;
                    case "Playlists":
                        ConvertDocuments<Playlist>(col, PlaylistsVersion);
                        break;
                    case "Collections":
                        ConvertDocuments<Collection>(col, CollectionsVersion);
                        break;
                    case "Queues":
                        ConvertDocuments<PlayQueue>(col, QueuesVersion);
                        break;
                    case "Users":
                        ConvertDocuments<User>(col, UsersVersion);
                        break;
                    case "Stats":
                        ConvertDocuments<PlayStat>(col, StatsVersion);
                        break;
                }

                Console.WriteLine($"[+] {StringsManager.GetString("ConversionCompleted")}");
            }
        }
        public static void ConvertDocuments<T>(string col, string version)
        {
            List<KeyValuePair<string, BsonDocument>> results = new List<KeyValuePair<string, BsonDocument>>();
            Type targetType = typeof(T);

            var mongoDb = StateManager.DbClient.GetDatabase("Melon");
            var collection = mongoDb.GetCollection<BsonDocument>(col);
            var documents = collection.AsQueryable();

            foreach(var doc in documents)
            {
                dynamic dynDoc = BsonTypeMapper.MapToDotNetValue(doc);
                T targetInstance = Activator.CreateInstance<T>();

                foreach (PropertyInfo prop in targetType.GetProperties())
                {
                    if (doc != null && doc.Contains(prop.Name))
                    {
                        if (prop.PropertyType == typeof(DbLink))
                        {
                            prop.SetValue(targetInstance, new DbLink() { _id = dynDoc[prop.Name]["_id"], Name = dynDoc[prop.Name]["Name"] });
                        }
                        else if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string) && prop.PropertyType != typeof(byte[]))
                        {
                            var listType = typeof(List<>).MakeGenericType(new[] { prop.PropertyType.GetGenericArguments()[0] });
                            var resultList = (IList)Activator.CreateInstance(listType);
                            if(dynDoc[prop.Name] == null)
                            {
                                prop.SetValue(targetInstance, resultList);
                                continue;
                            }
                            foreach (var item in dynDoc[prop.Name])
                            {
                                if (prop.PropertyType.GetGenericArguments()[0] == typeof(DbLink))
                                {
                                    var link = new DbLink() { _id = item["_id"], Name = item["Name"] };
                                    resultList.Add(link);
                                }
                                else if (prop.PropertyType.GetGenericArguments()[0] == typeof(UserStat))
                                {
                                    var stat = new UserStat() { UserId = item["UserId"], Value = item["Value"] };
                                    resultList.Add(stat);
                                }
                                else if(prop.PropertyType.GetGenericArguments()[0] == typeof(string))
                                {
                                    string str = item;
                                    resultList.Add(str);
                                }
                            }
                            prop.SetValue(targetInstance, resultList);
                        }
                        else 
                        {
                            prop.SetValue(targetInstance, dynDoc[prop.Name]);
                        }
                    }
                    else
                    {
                        if (prop.PropertyType == typeof(string))
                        {
                            prop.SetValue(targetInstance, string.Empty);
                        }
                        else if(prop.PropertyType == typeof(bool))
                        {
                            prop.SetValue(targetInstance, false);
                        }
                        else if(prop.PropertyType == typeof(DateTime))
                        {
                            prop.SetValue(targetInstance, DateTime.MinValue);
                        }
                        else if(prop.PropertyType == typeof(int) || prop.PropertyType == typeof(long) || prop.PropertyType == typeof(double))
                        {
                            prop.SetValue(targetInstance, 0);
                        }
                        else if(prop.PropertyType == typeof(DbLink))
                        {
                            prop.SetValue(targetInstance, new DbLink() { _id = "", Name = "" });
                        }
                        else if (prop.PropertyType.IsArray)
                        {
                            prop.SetValue(targetInstance, Array.CreateInstance(prop.PropertyType.GetElementType(), 0));
                        }
                        else if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
                        {
                            Type listType = typeof(List<>).MakeGenericType(prop.PropertyType.GetGenericArguments());
                            prop.SetValue(targetInstance, Activator.CreateInstance(listType));
                        }
                    }
                }

                results.Add(KeyValuePair.Create(doc["_id"].ToString(), targetInstance.ToBsonDocument()));
            }

            var models = new List<WriteModel<BsonDocument>>();
            foreach (var doc in results)
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc.Key);
                var update = Builders<BsonDocument>.Update.Set(a => a, doc.Value);

                models.Add(new ReplaceOneModel<BsonDocument>(filter, doc.Value) { IsUpsert = true });
            }

            if (models.Count != 0)
            {
                collection.BulkWrite(models);
            }

            var metadata = mongoDb.GetCollection<DbMetadata>("Metadata");
            metadata.UpdateOne(Builders<DbMetadata>.Filter.Eq("Name", $"{col}Collection"), Builders<DbMetadata>.Update.Set(x=>x.Version, version));

        }
    }
}
