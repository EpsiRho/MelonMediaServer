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

namespace Melon.LocalClasses
{
    /// <summary>
    /// Functions for interacting with the database.
    /// </summary>
    public static class MelonAPI
    {

        public static async Task<List<Track>> SearchTracks(string query, int pageNumber, int pageSize)
        {
            var options = new RestClientOptions("https://localhost:7004/")
            {
                //Authenticator = new HttpBasicAuthenticator("username", "password")
            };

            var client = new RestClient(options);
            var request = new RestRequest("api/MelonSearch/Tracks");
            request.AddQueryParameter("query", query);
            request.AddQueryParameter("page", pageNumber);
            request.AddQueryParameter("count", pageSize);
            var response = await client.GetAsync(request);

            return JsonConvert.DeserializeObject<List<Track>>(response.Content);
        }
        public static List<Track> ShuffleTracks(List<Track> tracks, ShuffleType type, bool FullRandom = false)
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
                    for (int l = 0; l < 5; l++)
                    {
                        var count = tracks.Count();
                        for (int i = 0; i < tracks.Count - 1; i++)
                        {
                            if (tracks[i].TrackArtists.Contains(tracks[i + 1].TrackArtists[0]) || tracks[i].AlbumName == tracks[i + 1].AlbumName)
                            {
                                var temp = tracks[i];
                                tracks.RemoveAt(i);
                                var k = rng.Next(0, count);
                                tracks.Insert(k, temp);
                            }
                        }
                    }
                    return tracks;

                // Shuffle By Album
                case ShuffleType.ByAlbum:
                    var albumDic = new Dictionary<string, List<Track>>();

                    foreach (var track in tracks)
                    {
                        if (albumDic.ContainsKey(track.AlbumName))
                        {
                            for(int i = 0; i < albumDic[track.AlbumName].Count ; i++)
                            {
                                if (albumDic[track.AlbumName][i].Disc > track.Disc)
                                {
                                    albumDic[track.AlbumName].Insert(i, track);
                                    break;
                                }
                                else if (albumDic[track.AlbumName][i].Disc == track.Disc && albumDic[track.AlbumName][i].Position > track.Position)
                                {
                                    albumDic[track.AlbumName].Insert(i, track);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            albumDic.Add(track.AlbumName, new List<Track>() { track });
                        }
                    }

                    albumDic = albumDic.OrderBy(x => rng.Next()).ToDictionary(item => item.Key, item => item.Value);

                    var newTracks = new List<Track>();
                    foreach(var album in albumDic)
                    {
                        newTracks.AddRange(album.Value);
                    }

                    return newTracks;

                // Shuffle By Artist
                case ShuffleType.ByArtistRandom:
                    
                    return tracks;
            }

            return null;
        }
    }
}
