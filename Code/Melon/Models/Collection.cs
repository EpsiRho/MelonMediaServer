using Melon.LocalClasses;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Melon.Models
{
    public class Collection
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long TrackCount { get; set; }
        public string Owner { get; set; }
        public List<string> Editors { get; set; }
        public List<string> Viewers { get; set; }
        public bool PublicViewing { get; set; }
        public bool PublicEditing { get; set; }
        public string ArtworkPath { get; set; }
        public List<string> AndFilters { get; set; }
        public List<string> OrFilters { get; set; }
        public List<DbLink> Tracks { get; set; }
        public static List<DbLink> FindTracks(List<string> AndFilters, List<string> OrFilters, string UserId)
        {
            if (AndFilters.Count() == 0 && OrFilters.Count() == 0)
            {
                return new List<DbLink>();
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            List<FilterDefinition<Track>> AndDefs = new List<FilterDefinition<Track>>();
            foreach (var filter in AndFilters)
            {
                string property = filter.Split(";")[0];
                string type = filter.Split(";")[1];
                object value = filter.Split(";")[2];

                if (property.Contains("PlayCounts") || property.Contains("SkipCounts") || property.Contains("Ratings"))
                {
                    if (type == "Contains")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        AndDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Eq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        AndDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "NotEq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Ne(x => x.Value, value));
                        AndDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Lt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Lt(x => x.Value, value));
                        AndDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Gt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Gt(x => x.Value, value));
                        AndDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                }
                else
                {
                    if (property.Contains("Date") || property.Contains("Modified"))
                    {
                        try
                        {
                            value = DateTime.Parse(value.ToString());
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    }
                    if (type == "Contains")
                    {
                        AndDefs.Add(Builders<Track>.Filter.Regex(property, new BsonRegularExpression(value.ToString(), "i")));
                    }
                    else if (type == "Eq")
                    {
                        AndDefs.Add(Builders<Track>.Filter.Eq(property, value));
                    }
                    else if (type == "NotEq")
                    {
                        AndDefs.Add(Builders<Track>.Filter.Ne(property, value));
                    }
                    else if (type == "Lt")
                    {
                        AndDefs.Add(Builders<Track>.Filter.Lt(property, value));
                    }
                    else if (type == "Gt")
                    {
                        AndDefs.Add(Builders<Track>.Filter.Gt(property, value));
                    }
                }
            }

            List<FilterDefinition<Track>> OrDefs = new List<FilterDefinition<Track>>();
            foreach (var filter in OrFilters)
            {
                string property = filter.Split(";")[0];
                string type = filter.Split(";")[1];
                object value = filter.Split(";")[2];

                if (property.Contains("PlayCounts") || property.Contains("SkipCounts") || property.Contains("Ratings"))
                {
                    if (type == "Contains")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        OrDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Eq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        OrDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "NotEq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Ne(x => x.Value, value));
                        OrDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Lt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Lt(x => x.Value, value));
                        OrDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Gt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Gt(x => x.Value, value));
                        OrDefs.Add(Builders<Track>.Filter.ElemMatch(property, f));
                    }
                }
                else
                {
                    if (property.Contains("Date") || property.Contains("Modified"))
                    {
                        try
                        {
                            value = DateTime.Parse(value.ToString());
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    }
                    if (type == "Contains")
                    {
                        OrDefs.Add(Builders<Track>.Filter.Regex(property, new BsonRegularExpression(value.ToString(), "i")));
                    }
                    else if (type == "Eq")
                    {
                        OrDefs.Add(Builders<Track>.Filter.Eq(property, value));
                    }
                    else if (type == "NotEq")
                    {
                        OrDefs.Add(Builders<Track>.Filter.Ne(property, value));
                    }
                    else if (type == "Lt")
                    {
                        OrDefs.Add(Builders<Track>.Filter.Lt(property, value));
                    }
                    else if (type == "Gt")
                    {
                        OrDefs.Add(Builders<Track>.Filter.Gt(property, value));
                    }
                }
            }

            FilterDefinition<Track> combinedFilter = null;
            foreach (var filter in AndDefs)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    combinedFilter = Builders<Track>.Filter.And(combinedFilter, filter);
                }
            }
            foreach (var filter in OrDefs)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    combinedFilter = Builders<Track>.Filter.Or(combinedFilter, filter);
                }
            }

            try
            {
                var trackProjection = Builders<Track>.Projection.Include(x => x._id)
                                                                .Include(x => x.TrackName);
                var trackDocs = TracksCollection.Find(combinedFilter)
                                                .Project(trackProjection)
                                                .ToList()
                                                .Select(x => new DbLink() { _id = x["_id"].ToString(), Name = x["TrackName"].ToString() })
                                                .ToList();
                return trackDocs;
            }
            catch (Exception)
            {

            }
            return null;
        }
    }
    public class ResponseCollection
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long TrackCount { get; set; }
        public string Owner { get; set; }
        public List<string> Editors { get; set; }
        public List<string> Viewers { get; set; }
        public bool PublicViewing { get; set; }
        public bool PublicEditing { get; set; }
        public List<string> AndFilters { get; set; }
        public List<string> OrFilters { get; set; }
    }


}
