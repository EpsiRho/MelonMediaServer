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
        public static List<ShortTrack> ShuffleTracks(List<ShortTrack> tracks, ShuffleType type, bool FullRandom = false)
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
                        ShortTrack value = tracks[k];
                        tracks[k] = tracks[n];
                        tracks[n] = value;
                    }

                    // Remove any consecutive tracks from the same artist or album.
                    if (!FullRandom)
                    {
                        for (int l = 0; l < 5; l++)
                        {
                            var count = tracks.Count();
                            for (int i = 0; i < tracks.Count - 1; i++)
                            {
                                if (tracks[i].TrackArtists.Contains(tracks[i + 1].TrackArtists[0]) || tracks[i].Album.AlbumName == tracks[i + 1].Album.AlbumName)
                                {
                                    var temp = tracks[i];
                                    tracks.RemoveAt(i);
                                    var k = rng.Next(0, count);
                                    tracks.Insert(k, temp);
                                }
                            }
                        }
                    }
                    return tracks;

                // Shuffle By Album
                case ShuffleType.ByAlbum:
                    var albumDic = new Dictionary<string, List<ShortTrack>>();

                    foreach (var track in tracks)
                    {
                        if (albumDic.ContainsKey(track.Album.AlbumId))
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
                            albumDic[track.Album.AlbumId].Add(track);
                        }
                        else
                        {
                            albumDic.Add(track.Album.AlbumId, new List<ShortTrack>() { track });
                        }
                    }

                    albumDic = albumDic.OrderBy(x => rng.Next()).ToDictionary(item => item.Key, item => item.Value);

                    var newTracks = new List<ShortTrack>();
                    foreach(var album in albumDic)
                    {
                        var tks = album.Value;
                        if (FullRandom)
                        {
                            int number = tks.Count;
                            while (number > 1)
                            {
                                number--;
                                int k = rng.Next(number + 1);
                                ShortTrack value = tks[k];
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

                    return newTracks;

                // Shuffle By Artist
                case ShuffleType.ByArtistRandom:
                    var artistDic = new Dictionary<string, List<ShortTrack>>();

                    foreach (var track in tracks)
                    {
                        if (artistDic.ContainsKey(track.TrackArtists[0].ArtistId))
                        {
                            artistDic[track.TrackArtists[0].ArtistId].Add(track);
                        }
                        else
                        {
                            artistDic.Add(track.TrackArtists[0].ArtistId, new List<ShortTrack>() { track });
                        }
                    }

                    artistDic = artistDic.OrderBy(x => rng.Next()).ToDictionary(item => item.Key, item => item.Value);

                    var nTracks = new List<ShortTrack>();
                    foreach (var album in artistDic)
                    {
                        var tks = album.Value;
                        int nm = tks.Count;
                        while (nm > 1)
                        {
                            nm--;
                            int k = rng.Next(nm + 1);
                            ShortTrack value = tks[k];
                            tks[k] = tks[nm];
                            tks[nm] = value;
                        }

                        nTracks.AddRange(tks);
                    }
                    return nTracks;
                case ShuffleType.ByTrackFavorites:
                    var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
                    var mongoDatabase = mongoClient.GetDatabase("Melon");
                    var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

                    List<Track> fullTracks = new List<Track>();
                    foreach (var track in tracks)
                    {
                        var trackFilter = Builders<Track>.Filter.Eq("_id", track._id);
                        var t = TracksCollection.Find(trackFilter).FirstOrDefault();
                        fullTracks.Add(t);
                    }

                    Random rand = new Random();

                    // Sort with a bias towards PlayCount and Rating
                    fullTracks = fullTracks.OrderByDescending(x => x.PlayCount + x.Rating + rand.NextDouble()).ToList();

                    //int num = fullTracks.Count;
                    //for (int i = 0; i < num; i++)
                    //{
                    //    int r = i + rand.Next(num - i);
                    //    var temp = fullTracks[i];
                    //    fullTracks[i] = fullTracks[r];
                    //    fullTracks[r] = temp;
                    //}

                    List<ShortTrack> finalTracks = new List<ShortTrack>();
                    foreach(var track in fullTracks)
                    {
                        finalTracks.Add(new ShortTrack(track));
                    }

                    return finalTracks;
                case ShuffleType.ByTrackDiscovery:
                    var mc = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
                    var md = mc.GetDatabase("Melon");
                    var TCollection = md.GetCollection<Track>("Tracks");

                    List<Track> fTracks = new List<Track>();
                    foreach (var track in tracks)
                    {
                        var trackFilter = Builders<Track>.Filter.Eq("_id", track._id);
                        var t = TCollection.Find(trackFilter).FirstOrDefault();
                        fTracks.Add(t);
                    }

                    Random r = new Random();

                    // Sort with a bias against PlayCount and Rating
                    fTracks = fTracks.OrderBy(x => x.PlayCount + x.Rating + r.NextDouble()).ToList();

                    //int num = fullTracks.Count;
                    //for (int i = 0; i < num; i++)
                    //{
                    //    int r = i + rand.Next(num - i);
                    //    var temp = fullTracks[i];
                    //    fullTracks[i] = fullTracks[r];
                    //    fullTracks[r] = temp;
                    //}

                    List<ShortTrack> outTracks = new List<ShortTrack>();
                    foreach (var track in fTracks)
                    {
                        outTracks.Add(new ShortTrack(track));
                    }

                    return outTracks;
            }

            return null;
        }
    }
}
