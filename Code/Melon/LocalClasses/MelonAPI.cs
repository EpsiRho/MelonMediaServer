using Melon.Types;
using Microsoft.Owin.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Melon.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Melon.Classes;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;

namespace Melon.LocalClasses
{
    /// <summary>
    /// Functions for interacting with the database.
    /// </summary>
    public static class MelonAPI
    {
        public static List<Track> ShuffleTracks(List<Track> tracks, string UserId, ShuffleType type, bool fullRandom = false, bool enableTrackLinks = true)
        {
            Random rng = new Random();
            // Shuffle the list.
            switch (type) 
            {
                // Shuffle By Track
                case ShuffleType.ByTrack:
                    int n = tracks.Count;
                    while (n > 1)
                    {
                        n--;
                        int k = rng.Next(n + 1);
                        Track value = tracks[k];
                        tracks[k] = tracks[n];
                        tracks[n] = value;
                    }

                    // Remove any consecutive tracks from the same artist or album.
                    if (!fullRandom)
                    {
                        for (int l = 0; l < 5; l++)
                        {
                            var count = tracks.Count();
                            for (int i = 0; i < count - 1; i++)
                            {
                                if (tracks[i].TrackArtists.Contains(tracks[i + 1].TrackArtists[0]) || tracks[i].Album.Name == tracks[i + 1].Album.Name)
                                {
                                    var temp = tracks[i];
                                    tracks.RemoveAt(i);
                                    var k = rng.Next(0, count);
                                    tracks.Insert(k, temp);
                                }
                            }
                        }
                    }

                    if (!enableTrackLinks)
                    {
                        return tracks;
                    }

                    // Find track links and connect them
                    for (int i = 0; i < tracks.Count() - 1; i++)
                    {
                        if (tracks[i].nextTrack != "")
                        {
                            for (int j = 0; j < tracks.Count(); j++)
                            {
                                if (tracks[j]._id == tracks[i].nextTrack)
                                {
                                    var temp = tracks[i+1];
                                    tracks[i + 1] = tracks[j];
                                    tracks[j] = temp;
                                    break;
                                }
                            }
                        }
                    }

                    return tracks;

                // Shuffle By Album
                case ShuffleType.ByAlbum:
                    var albumDic = new Dictionary<string, List<Track>>();

                    foreach (var track in tracks)
                    {
                        if (albumDic.ContainsKey(track.Album._id))
                        {
                            //for(int i = 0; i < albumDic[track.Album.AlbumId].Count ; i++)
                            //{
                            //    if (albumDic[track.Album.AlbumId][i].Disc > track.Disc)
                            //    {
                            //        albumDic[track.Album.AlbumId].Insert(i, track);
                            //        break;
                            //    }
                            //    else if (albumDic[track.Album.AlbumId][i].Disc == track.Disc && albumDic[track.Album.AlbumId][i].Position > track.Position)
                            //    {
                            //        albumDic[track.Album.AlbumId].Insert(i, track);
                            //        break;
                            //    }
                            //}
                            albumDic[track.Album._id].Add(track);
                        }
                        else
                        {
                            albumDic.Add(track.Album._id, new List<Track>() { track });
                        }
                    }

                    albumDic = albumDic.OrderBy(x => rng.Next()).ToDictionary(item => item.Key, item => item.Value);

                    var newTracks = new List<Track>();
                    foreach(var album in albumDic)
                    {
                        var tks = album.Value;
                        if (fullRandom)
                        {
                            int number = tks.Count;
                            while (number > 1)
                            {
                                number--;
                                int k = rng.Next(number + 1);
                                Track value = tks[k];
                                tks[k] = tks[number];
                                tks[number] = value;
                            }
                        }
                        else
                        {
                            tks = tks.OrderBy(x => x.Position + x.Disc).ToList();
                        }
                        newTracks.AddRange(tks);
                    }

                    if (fullRandom && enableTrackLinks)
                    {
                        // Find track links and connect them
                        for (int i = 0; i < newTracks.Count() - 1; i++)
                        {
                            if (newTracks[i].nextTrack != "")
                            {
                                for (int j = 0; j < newTracks.Count(); j++)
                                {
                                    if (newTracks[j]._id == newTracks[i].nextTrack)
                                    {
                                        var temp = newTracks[i + 1];
                                        newTracks[i + 1] = newTracks[j];
                                        newTracks[j] = temp;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    return newTracks;

                // Shuffle By Artist
                case ShuffleType.ByArtistRandom:
                    var artistDic = new Dictionary<string, List<Track>>();

                    foreach (var track in tracks)
                    {
                        if (artistDic.ContainsKey(track.TrackArtists[0]._id))
                        {
                            artistDic[track.TrackArtists[0]._id].Add(track);
                        }
                        else
                        {
                            artistDic.Add(track.TrackArtists[0]._id, new List<Track>() { track });
                        }
                    }

                    artistDic = artistDic.OrderBy(x => rng.Next()).ToDictionary(item => item.Key, item => item.Value);

                    var nTracks = new List<Track>();
                    foreach (var album in artistDic)
                    {
                        var tks = album.Value;
                        int nm = tks.Count;
                        while (nm > 1)
                        {
                            nm--;
                            int k = rng.Next(nm + 1);
                            Track value = tks[k];
                            tks[k] = tks[nm];
                            tks[nm] = value;
                        }

                        nTracks.AddRange(tks);
                    }

                    if (!enableTrackLinks)
                    {
                        return nTracks;
                    }

                    // Find track links and connect them
                    for (int i = 0; i < nTracks.Count() - 1; i++)
                    {
                        if (nTracks[i].nextTrack != "")
                        {
                            for (int j = 0; j < nTracks.Count(); j++)
                            {
                                if (nTracks[j]._id == nTracks[i].nextTrack)
                                {
                                    var temp = nTracks[i + 1];
                                    nTracks[i + 1] = nTracks[j];
                                    nTracks[j] = temp;
                                    break;
                                }
                            }
                        }
                    }

                    return nTracks;
                case ShuffleType.ByTrackFavorites:
                    var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
                    var mongoDatabase = mongoClient.GetDatabase("Melon");
                    var TracksCollection = mongoDatabase.GetCollection<Artist>("Tracks");

                    Random rand = new Random();

                    // Sort with a bias towards PlayCount and Rating
                    var fullTracks = tracks.OrderByDescending(x => x.PlayCounts.Where(x=>x.UserId == UserId).Select(x=>x.Value).FirstOrDefault() + 
                                                                   x.Ratings.Where(x => x.UserId == UserId).Select(x => x.Value).FirstOrDefault() - 
                                                                   x.SkipCounts.Where(x => x.UserId == UserId).Select(x => x.Value).FirstOrDefault() + 
                                                                   rand.NextDouble()).ToList();

                    List<Track> finalTracks = new List<Track>(fullTracks);

                    if (!enableTrackLinks)
                    {
                        return finalTracks;
                    }

                    // Find track links and connect them
                    for (int i = 0; i < finalTracks.Count() - 1; i++)
                    {
                        if (finalTracks[i].nextTrack != "")
                        {
                            for (int j = 0; j < finalTracks.Count(); j++)
                            {
                                if (finalTracks[j]._id == finalTracks[i].nextTrack)
                                {
                                    var temp = finalTracks[i + 1];
                                    finalTracks[i + 1] = finalTracks[j];
                                    finalTracks[j] = temp;
                                    break;
                                }
                            }
                        }
                    }

                    return finalTracks;
                case ShuffleType.ByTrackDiscovery:
                    var mc = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
                    var md = mc.GetDatabase("Melon");
                    var TCollection = md.GetCollection<Track>("Tracks");

                    List<Track> fTracks = new List<Track>();
                    foreach (var track in tracks)
                    {
                        var trackFilter = Builders<Track>.Filter.Eq(x=>x._id, track._id);
                        var t = TCollection.Find(trackFilter).FirstOrDefault();
                        fTracks.Add(t);
                    }

                    Random r = new Random();

                    // Sort with a bias against PlayCount
                    fTracks = fTracks.OrderBy(x => x.PlayCounts.Where(x => x.UserId == UserId).Select(x => x.Value).FirstOrDefault() +
                                                   x.Ratings.Where(x => x.UserId == UserId).Select(x => x.Value).FirstOrDefault() -
                                                   x.SkipCounts.Where(x => x.UserId == UserId).Select(x => x.Value).FirstOrDefault() +
                                                   r.NextDouble()).ToList();

                    List<Track> outTracks = new List<Track>(fTracks);

                    if (!enableTrackLinks)
                    {
                        return outTracks;
                    }

                    // Find track links and connect them
                    for (int i = 0; i < outTracks.Count() - 1; i++)
                    {
                        if (outTracks[i].nextTrack != "")
                        {
                            for (int j = 0; j < outTracks.Count(); j++)
                            {
                                if (outTracks[j]._id == outTracks[i].nextTrack)
                                {
                                    var temp = outTracks[i + 1];
                                    outTracks[i + 1] = outTracks[j];
                                    outTracks[j] = temp;
                                    break;
                                }
                            }
                        }
                    }

                    return outTracks;
            }

            return null;
        }
        public static List<Track> FindTracks(List<string> AndFilters, List<string> OrFilters, string UserId, int page, int count, string sort = "NameAsc")
        {
            if(AndFilters == null)
            {
                AndFilters = new List<string>();
            }

            if(OrFilters == null)
            {
                OrFilters = new List<string>();
            }

            if (AndFilters.Count() == 0 && OrFilters.Count() == 0)
            {
                return new List<Track>();
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var PlaylistsCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

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
                else if (property.Contains("Playlist"))
                {
                    if (type == "Contains")
                    {
                        var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, value)).FirstOrDefault();
                        if (playlist == null)
                        {
                            continue;
                        }

                        var tracks = playlist.Tracks.Select(x => x._id).ToList();
                        AndDefs.Add(Builders<Track>.Filter.In(x => x._id, tracks));
                    }
                }
                else if (property.Contains("TrackArtists"))
                {
                    if (type == "Eq")
                    {
                        var f = Builders<Track>.Filter.ElemMatch(x => x.TrackArtists, artist => artist._id == value);
                        AndDefs.Add(f);
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
                else if (property.Contains("Playlist"))
                {
                    if (type == "Contains")
                    {
                        var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, value)).FirstOrDefault();
                        if (playlist == null)
                        {
                            continue;
                        }

                        var tracks = playlist.Tracks.Select(x => x._id).ToList();
                        OrDefs.Add(Builders<Track>.Filter.In(x => x._id, tracks));
                    }
                }
                else if (property.Contains("TrackArtists"))
                {
                    if (type == "Eq")
                    {
                        var f = Builders<Track>.Filter.ElemMatch(x => x.TrackArtists, artist => artist._id == value);
                        OrDefs.Add(f);
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
                SortDefinition<Track> sortDefinition = null;
                bool needsAggregate = false;
                switch (sort)
                {
                    case "NameDesc":
                        sortDefinition = Builders<Track>.Sort.Descending(x => x.Name);
                        break;
                    case "NameAsc":
                        sortDefinition = Builders<Track>.Sort.Ascending(x => x.Name);
                        break;
                    case "DateAddedDesc":
                        sortDefinition = Builders<Track>.Sort.Descending(x => x.DateAdded);
                        break;
                    case "DateAddedAsc":
                        sortDefinition = Builders<Track>.Sort.Ascending(x => x.DateAdded);
                        break;
                    case "ReleaseDateDesc":
                        sortDefinition = Builders<Track>.Sort.Descending(x => x.ReleaseDate);
                        break;
                    case "ReleaseDateAsc":
                        sortDefinition = Builders<Track>.Sort.Ascending(x => x.ReleaseDate);
                        break;
                    case "PlayCountDesc":
                        needsAggregate = true;
                        break;
                    case "PlayCountAsc":
                        needsAggregate = true;
                        break;
                    case "AlbumPositionAsc":
                        sortDefinition = Builders<Track>.Sort.Ascending(x => x.Disc).Ascending(x=>x.Position);
                        break;
                    case "AlbumPositionDesc":
                        sortDefinition = Builders<Track>.Sort.Descending(x => x.Disc).Descending(x=>x.Position);
                        break;
                }

                if (needsAggregate)
                {
                    combinedFilter = Builders<Track>.Filter.And(combinedFilter, Builders<Track>.Filter.ElemMatch(x => x.PlayCounts, Builders<UserStat>.Filter.Eq(x => x.UserId, UserId)));
                    var responseTrackProjection = new BsonDocument {
                        { "_id", 1 },
                        { "Album", 1 },
                        { "Position", 1 },
                        { "Disc", 1 },
                        { "Format", 1 },
                        { "Bitrate", 1 },
                        { "SampleRate", 1 },
                        { "Channels", 1 },
                        { "BitsPerSample", 1 },
                        { "MusicBrainzID", 1 },
                        { "ISRC", 1 },
                        { "Year", 1 },
                        { "Name", 1 },
                        { "Duration", 1 },
                        { "nextTrack", 1 },
                        { "SkipCounts", 1 },
                        { "Ratings", 1 },
                        { "TrackArtCount", 1 },
                        { "TrackArtDefault", 1 },
                        { "ServerURL", 1 },
                        { "LastModified", 1 },
                        { "DateAdded", 1 },
                        { "ReleaseDate", 1 },
                        { "Chapters", 1 },
                        { "TrackGenres", 1 },
                        { "TrackArtists", 1 },
                        { "PlayCounts", 1 },
                        { "SortValue", new BsonDocument("$let", new BsonDocument {
                            { "vars", new BsonDocument("filteredItems", new BsonDocument("$filter", new BsonDocument {
                                { "input", "$PlayCounts" },
                                { "as", "item" },
                                { "cond", new BsonDocument("$eq", new BsonArray { "$$item.UserId", UserId }) }
                            })) },
                            { "in", new BsonDocument("$arrayElemAt", new BsonArray { "$$filteredItems.Value", 0 }) }
                        }) }
                    };
                    var projection = Builders<BsonDocument>.Projection.Include("Album")
                    .Include("Position")
                    .Include("Disc")
                    .Include("Format")
                    .Include("Bitrate")
                    .Include("SampleRate")
                    .Include("Channels")
                    .Include("BitsPerSample")
                    .Include("MusicBrainzID")
                    .Include("ISRC")
                    .Include("Year")
                    .Include("Name")
                    .Include("Duration")
                    .Include("nextTrack")
                    .Include("PlayCounts")
                    .Include("SkipCounts")
                    .Include("Ratings")
                    .Include("TrackArtCount")
                    .Include("TrackArtDefault")
                    .Include("ServerURL")
                    .Include("LastModified")
                    .Include("DateAdded")
                    .Include("ReleaseDate")
                    .Include("Chapters")
                    .Include("TrackGenres")
                    .Include("TrackArtists")
                    .Include("_id");
                    switch (sort)
                    {
                        case "PlayCountDesc":
                            var pipeline = TracksCollection.Aggregate()
                                            .Match(combinedFilter)
                                            .Project(responseTrackProjection)
                                            .Sort(new BsonDocument("SortValue", -1))
                                            .Skip(page * count)
                                            .Limit(count)
                                            .Project<Track>(projection);

                            return pipeline.ToList();
                        case "PlayCountAsc":
                            var pipelineAsc = TracksCollection.Aggregate()
                                            .Match(combinedFilter)
                                            .Project(responseTrackProjection)
                                            .Sort(new BsonDocument("SortValue", 1))
                                            .Skip(page * count)
                                            .Limit(count)
                                            .Project<Track>(projection);

                            return pipelineAsc.ToList();
                    }
                }

                if (page == -1 || count == -1)
                {
                    return TracksCollection.Find(combinedFilter)
                                           .Sort(sortDefinition)
                                           .ToList();
                }
                return TracksCollection.Find(combinedFilter)
                                       .Skip(page * count)
                                       .Sort(sortDefinition)
                                       .Limit(count)
                                       .ToList();
            }
            catch (Exception)
            {

            }
            return null;
        }

        public static List<Album> FindAlbums(List<string> AndFilters, List<string> OrFilters, string UserId, int page, int count, string sort = "NameAsc")
        {
            if (AndFilters == null)
            {
                AndFilters = new List<string>();
            }

            if (OrFilters == null)
            {
                OrFilters = new List<string>();
            }

            if (AndFilters.Count() == 0 && OrFilters.Count() == 0)
            {
                return new List<Album>();
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var PlaylistsCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            List<FilterDefinition<Album>> AndDefs = new List<FilterDefinition<Album>>();
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
                        AndDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Eq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        AndDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "NotEq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Ne(x => x.Value, value));
                        AndDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Lt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Lt(x => x.Value, value));
                        AndDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Gt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Gt(x => x.Value, value));
                        AndDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                }
                else if (property.Contains("Playlist"))
                {
                    //if (type == "Contains")
                    //{
                    //    var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, value)).FirstOrDefault();
                    //    if (playlist == null)
                    //    {
                    //        continue;
                    //    }
                    //
                    //    var tracks = playlist.Tracks.Select(x => x._id).ToList();
                    //    AndDefs.Add(Builders<Album>.Filter.AnyIn(x => x.Tracks.Select(x=>x._id), tracks));
                    //}
                }
                else if (property.Contains("AlbumArtists"))
                {
                    if (type == "Eq")
                    {
                        var f = Builders<Album>.Filter.ElemMatch(x => x.AlbumArtists, artist => artist._id == value);
                        AndDefs.Add(f);
                    }
                }
                else if (property.Contains("ContributingArtists"))
                {
                    if (type == "Eq")
                    {
                        var f = Builders<Album>.Filter.ElemMatch(x => x.ContributingArtists, artist => artist._id == value);
                        AndDefs.Add(f);
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
                        AndDefs.Add(Builders<Album>.Filter.Regex(property, new BsonRegularExpression(value.ToString(), "i")));
                    }
                    else if (type == "Eq")
                    {
                        AndDefs.Add(Builders<Album>.Filter.Eq(property, value));
                    }
                    else if (type == "NotEq")
                    {
                        AndDefs.Add(Builders<Album>.Filter.Ne(property, value));
                    }
                    else if (type == "Lt")
                    {
                        AndDefs.Add(Builders<Album>.Filter.Lt(property, value));
                    }
                    else if (type == "Gt")
                    {
                        AndDefs.Add(Builders<Album>.Filter.Gt(property, value));
                    }
                }
            }

            List<FilterDefinition<Album>> OrDefs = new List<FilterDefinition<Album>>();
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
                        OrDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Eq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        OrDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "NotEq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Ne(x => x.Value, value));
                        OrDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Lt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Lt(x => x.Value, value));
                        OrDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Gt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Gt(x => x.Value, value));
                        OrDefs.Add(Builders<Album>.Filter.ElemMatch(property, f));
                    }
                }
                else if (property.Contains("Playlist"))
                {
                    //if (type == "Contains")
                    //{
                    //    var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, value)).FirstOrDefault();
                    //    if (playlist == null)
                    //    {
                    //        continue;
                    //    }
                    //
                    //    var tracks = playlist.Tracks.Select(x => x._id).ToList();
                    //    OrDefs.Add(Builders<Album>.Filter.AnyIn(x => x.Tracks.Select(x => x._id), tracks));
                    //}
                }
                else if (property.Contains("AlbumArtists"))
                {
                    if (type == "Eq")
                    {
                        var f = Builders<Album>.Filter.ElemMatch(x => x.AlbumArtists, artist => artist._id == value);
                        OrDefs.Add(f);
                    }
                }
                else if (property.Contains("ContributingArtists"))
                {
                    if (type == "Eq")
                    {
                        var f = Builders<Album>.Filter.ElemMatch(x => x.ContributingArtists, artist => artist._id == value);
                        OrDefs.Add(f);
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
                        OrDefs.Add(Builders<Album>.Filter.Regex(property, new BsonRegularExpression(value.ToString(), "i")));
                    }
                    else if (type == "Eq")
                    {
                        OrDefs.Add(Builders<Album>.Filter.Eq(property, value));
                    }
                    else if (type == "NotEq")
                    {
                        OrDefs.Add(Builders<Album>.Filter.Ne(property, value));
                    }
                    else if (type == "Lt")
                    {
                        OrDefs.Add(Builders<Album>.Filter.Lt(property, value));
                    }
                    else if (type == "Gt")
                    {
                        OrDefs.Add(Builders<Album>.Filter.Gt(property, value));
                    }
                }
            }

            FilterDefinition<Album> combinedFilter = null;
            foreach (var filter in AndDefs)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    combinedFilter = Builders<Album>.Filter.And(combinedFilter, filter);
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
                    combinedFilter = Builders<Album>.Filter.Or(combinedFilter, filter);
                }
            }

            try
            {
                SortDefinition<Album> sortDefinition = null;
                bool needsAggregate = false;
                switch (sort)
                {
                    case "NameDesc":
                        sortDefinition = Builders<Album>.Sort.Descending(x => x.Name);
                        break;
                    case "NameAsc":
                        sortDefinition = Builders<Album>.Sort.Ascending(x => x.Name);
                        break;
                    case "DateAddedDesc":
                        sortDefinition = Builders<Album>.Sort.Descending(x => x.DateAdded);
                        break;
                    case "DateAddedAsc":
                        sortDefinition = Builders<Album>.Sort.Ascending(x => x.DateAdded);
                        break;
                    case "ReleaseDateDesc":
                        sortDefinition = Builders<Album>.Sort.Descending(x => x.ReleaseDate);
                        break;
                    case "ReleaseDateAsc":
                        sortDefinition = Builders<Album>.Sort.Ascending(x => x.ReleaseDate);
                        break;
                    case "PlayCountDesc":
                        needsAggregate = true;
                        break;
                    case "PlayCountAsc":
                        needsAggregate = true;
                        break;
                }

                if (needsAggregate)
                {
                    combinedFilter = Builders<Album>.Filter.And(combinedFilter, Builders<Album>.Filter.ElemMatch(x => x.PlayCounts, Builders<UserStat>.Filter.Eq(x => x.UserId, UserId)));
                    var responseAlbumProjection = new BsonDocument {
                        { "_id", 1 },
                        { "TotalDiscs", 1 },
                        { "TotalTracks", 1 },
                        { "Name", 1 },
                        { "Bio", 1 },
                        { "Publisher", 1 },
                        { "ReleaseStatus", 1 },
                        { "ReleaseType", 1 },
                        { "SkipCounts", 1 },
                        { "Ratings", 1 },
                        { "ServerURL", 1 },
                        { "DateAdded", 1 },
                        { "ReleaseDate", 1 },
                        { "AlbumArtCount", 1 },
                        { "AlbumArtDefault", 1 },
                        { "AlbumGenres", 1 },
                        { "AlbumArtists", 1 },
                        { "ContributingArtists", 1 },
                        { "PlayCounts", 1 },
                        { "SortValue", new BsonDocument("$let", new BsonDocument {
                            { "vars", new BsonDocument("filteredItems", new BsonDocument("$filter", new BsonDocument {
                                { "input", "$PlayCounts" },
                                { "as", "item" },
                                { "cond", new BsonDocument("$eq", new BsonArray { "$$item.UserId", UserId }) }
                            })) },
                            { "in", new BsonDocument("$arrayElemAt", new BsonArray { "$$filteredItems.Value", 0 }) }
                        }) }
                    };
                    var projection = Builders<BsonDocument>.Projection
                        .Include("_id")
                        .Include("TotalDiscs")
                        .Include("TotalTracks")
                        .Include("Name")
                        .Include("Bio")
                        .Include("Publisher")
                        .Include("ReleaseStatus")
                        .Include("ReleaseType")
                        .Include("PlayCounts")
                        .Include("SkipCounts")
                        .Include("Ratings")
                        .Include("ServerURL")
                        .Include("DateAdded")
                        .Include("ReleaseDate")
                        .Include("AlbumArtCount")
                        .Include("AlbumArtDefault")
                        .Include("AlbumGenres")
                        .Include("AlbumArtists")
                        .Include("ContributingArtists");

                    switch (sort)
                    {
                        case "PlayCountDesc":
                            var pipeline = AlbumsCollection.Aggregate()
                                            .Match(combinedFilter)
                                            .Project(responseAlbumProjection)
                                            .Sort(new BsonDocument("SortValue", -1))
                                            .Skip(page * count)
                                            .Limit(count)
                                            .Project<Album>(projection);

                            return pipeline.ToList();
                        case "PlayCountAsc":
                            var pipelineAsc = AlbumsCollection.Aggregate()
                                            .Match(combinedFilter)
                                            .Project(responseAlbumProjection)
                                            .Sort(new BsonDocument("SortValue", 1))
                                            .Skip(page * count)
                                            .Limit(count)
                                            .Project<Album>(projection);

                            return pipelineAsc.ToList();
                    }
                }

                if (page == -1 || count == -1)
                {
                    return AlbumsCollection.Find(combinedFilter)
                                           .Sort(sortDefinition)
                                           .ToList();
                }
                return AlbumsCollection.Find(combinedFilter)
                                        .Sort(sortDefinition)
                                        .Skip(page * count)
                                        .Limit(count)
                                        .ToList();
            }
            catch (Exception)
            {

            }
            return null;
        }
        public static List<Artist> FindArtists(List<string> AndFilters, List<string> OrFilters, string UserId, int page, int count, string sort = "NameAsc")
        {
            if (AndFilters == null)
            {
                AndFilters = new List<string>();
            }

            if (OrFilters == null)
            {
                OrFilters = new List<string>();
            }

            if (AndFilters.Count() == 0 && OrFilters.Count() == 0)
            {
                return new List<Artist>();
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var PlaylistsCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            List<FilterDefinition<Artist>> AndDefs = new List<FilterDefinition<Artist>>();
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
                        AndDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Eq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        AndDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "NotEq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Ne(x => x.Value, value));
                        AndDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Lt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Lt(x => x.Value, value));
                        AndDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Gt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Gt(x => x.Value, value));
                        AndDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                }
                //else if (property.Contains("Playlist"))
                //{
                //    if (type == "Contains")
                //    {
                //        var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, value)).FirstOrDefault();
                //        if (playlist == null)
                //        {
                //            continue;
                //        }
                //
                //        var tracks = playlist.Tracks.Select(x => x._id).ToList();
                //        AndDefs.Add(Builders<Artist>.Filter.AnyIn(x => x.Tracks.Select(x => x._id), tracks));
                //    }
                //}
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
                        AndDefs.Add(Builders<Artist>.Filter.Regex(property, new BsonRegularExpression(value.ToString(), "i")));
                    }
                    else if (type == "Eq")
                    {
                        AndDefs.Add(Builders<Artist>.Filter.Eq(property, value));
                    }
                    else if (type == "NotEq")
                    {
                        AndDefs.Add(Builders<Artist>.Filter.Ne(property, value));
                    }
                    else if (type == "Lt")
                    {
                        AndDefs.Add(Builders<Artist>.Filter.Lt(property, value));
                    }
                    else if (type == "Gt")
                    {
                        AndDefs.Add(Builders<Artist>.Filter.Gt(property, value));
                    }
                }
            }

            List<FilterDefinition<Artist>> OrDefs = new List<FilterDefinition<Artist>>();
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
                        OrDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Eq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Eq(x => x.Value, value));
                        OrDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "NotEq")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Ne(x => x.Value, value));
                        OrDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Lt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Lt(x => x.Value, value));
                        OrDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                    else if (type == "Gt")
                    {
                        var f = Builders<UserStat>.Filter.And(Builders<UserStat>.Filter.Eq(x => x.UserId, UserId), Builders<UserStat>.Filter.Gt(x => x.Value, value));
                        OrDefs.Add(Builders<Artist>.Filter.ElemMatch(property, f));
                    }
                }
                //else if (property.Contains("Playlist"))
                //{
                //    if (type == "Contains")
                //    {
                //        var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, value)).FirstOrDefault();
                //        if (playlist == null)
                //        {
                //            continue;
                //        }
                //
                //        var tracks = playlist.Tracks.Select(x => x._id).ToList();
                //        OrDefs.Add(Builders<Artist>.Filter.AnyIn(x => x.Tracks.Select(x => x._id), tracks));
                //    }
                //}
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
                        OrDefs.Add(Builders<Artist>.Filter.Regex(property, new BsonRegularExpression(value.ToString(), "i")));
                    }
                    else if (type == "Eq")
                    {
                        OrDefs.Add(Builders<Artist>.Filter.Eq(property, value));
                    }
                    else if (type == "NotEq")
                    {
                        OrDefs.Add(Builders<Artist>.Filter.Ne(property, value));
                    }
                    else if (type == "Lt")
                    {
                        OrDefs.Add(Builders<Artist>.Filter.Lt(property, value));
                    }
                    else if (type == "Gt")
                    {
                        OrDefs.Add(Builders<Artist>.Filter.Gt(property, value));
                    }
                }
            }

            FilterDefinition<Artist> combinedFilter = null;
            foreach (var filter in AndDefs)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    combinedFilter = Builders<Artist>.Filter.And(combinedFilter, filter);
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
                    combinedFilter = Builders<Artist>.Filter.Or(combinedFilter, filter);
                }
            }

            try
            {
                SortDefinition<Artist> sortDefinition = null;
                bool needsAggregate = false;
                switch (sort)
                {
                    case "NameDesc":
                        sortDefinition = Builders<Artist>.Sort.Descending(x => x.Name);
                        break;
                    case "NameAsc":
                        sortDefinition = Builders<Artist>.Sort.Ascending(x => x.Name);
                        break;
                    case "DateAddedDesc":
                        sortDefinition = Builders<Artist>.Sort.Descending(x => x.DateAdded);
                        break;
                    case "DateAddedAsc":
                        sortDefinition = Builders<Artist>.Sort.Ascending(x => x.DateAdded);
                        break;
                    case "PlayCountDesc":
                        needsAggregate = true;
                        break;
                    case "PlayCountAsc":
                        needsAggregate = true;
                        break;
                }

                if (needsAggregate)
                {
                    combinedFilter = Builders<Artist>.Filter.And(combinedFilter, Builders<Artist>.Filter.ElemMatch(x => x.PlayCounts, Builders<UserStat>.Filter.Eq(x => x.UserId, UserId)));
                    var responseAlbumProjection = new BsonDocument {
                        { "_id", 1 },
                        { "Name", 1 },
                        { "Bio", 1 },
                        { "ArtistPfpArtCount", 1 },
                        { "ArtistBannerArtCount", 1 },
                        { "ArtistPfpDefault", 1 },
                        { "ArtistBannerArtDefault", 1 },
                        { "SkipCounts", 1 },
                        { "Ratings", 1 },
                        { "ServerURL", 1 },
                        { "Genres", 1 },
                        { "DateAdded", 1 },
                        { "PlayCounts", 1 },
                        { "SortValue", new BsonDocument("$let", new BsonDocument {
                            { "vars", new BsonDocument("filteredItems", new BsonDocument("$filter", new BsonDocument {
                                { "input", "$PlayCounts" },
                                { "as", "item" },
                                { "cond", new BsonDocument("$eq", new BsonArray { "$$item.UserId", UserId }) }
                            })) },
                            { "in", new BsonDocument("$arrayElemAt", new BsonArray { "$$filteredItems.Value", 0 }) }
                        }) }
                    };
                    var projection = Builders<BsonDocument>.Projection
                        .Include("_id")
                        .Include("Name")
                        .Include("Bio")
                        .Include("ArtistPfpArtCount")
                        .Include("ArtistBannerArtCount")
                        .Include("ArtistPfpDefault")
                        .Include("ArtistBannerArtDefault")
                        .Include("PlayCounts")
                        .Include("SkipCounts")
                        .Include("Ratings")
                        .Include("ServerURL")
                        .Include("Genres")
                        .Include("DateAdded");
                    switch (sort)
                    {
                        case "PlayCountDesc":
                            var pipeline = ArtistsCollection.Aggregate()
                                            .Match(combinedFilter)
                                            .Project(responseAlbumProjection)
                                            .Sort(new BsonDocument("SortValue", -1))
                                            .Skip(page * count)
                                            .Limit(count)
                                            .Project<Artist>(projection);

                            return pipeline.ToList();
                        case "PlayCountAsc":
                            var pipelineAsc = ArtistsCollection.Aggregate()
                                            .Match(combinedFilter)
                                            .Project(responseAlbumProjection)
                                            .Sort(new BsonDocument("SortValue", 1))
                                            .Skip(page * count)
                                            .Limit(count)
                                            .Project<Artist>(projection);

                            return pipelineAsc.ToList();
                    }
                }

                if (page == -1 || count == -1)
                {
                    return ArtistsCollection.Find(combinedFilter)
                                           .Sort(sortDefinition)
                                           .ToList();
                }
                return ArtistsCollection.Find(combinedFilter)
                                        .Sort(sortDefinition)
                                        .Skip(page * count)
                                        .Limit(count)
                                        .ToList();
            }
            catch (Exception)
            {

            }
            return null;
        }

    }
}
