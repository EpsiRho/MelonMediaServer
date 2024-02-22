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
                    var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

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
        public static List<DbLink> FindTracks(List<string> AndFilters, List<string> OrFilters, string UserId)
        {
            if (AndFilters.Count() == 0 && OrFilters.Count() == 0)
            {
                return new List<DbLink>();
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
                                                                .Include(x => x.Name);
                var trackDocs = TracksCollection.Find(combinedFilter)
                                                .Project(trackProjection)
                                                .ToList()
                                                .Select(x => new DbLink() { _id = x["_id"].ToString(), Name = x["Name"].ToString() })
                                                .ToList();
                return trackDocs;
            }
            catch (Exception)
            {

            }
            return null;
        }
    }
}
