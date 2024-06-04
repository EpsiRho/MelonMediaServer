using Melon.Models;
using System.Xml.Linq;

namespace Melon.Models
{
    public class ProtoTrack
    {
        public string _id { get; set; }
        public Album Album { get; set; }
        public int Position { get; set; }
        public int Disc { get; set; }
        public string Format { get; set; }
        public string Bitrate { get; set; }
        public string SampleRate { get; set; }
        public string Channels { get; set; }
        public string BitsPerSample { get; set; }
        public string MusicBrainzID { get; set; }
        public string ISRC { get; set; }
        public string Year { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Duration { get; set; }
        public string nextTrack { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public int TrackArtCount { get; set; }
        public int TrackArtDefault { get; set; }
        public string LyricsPath { get; set; }
        public string ServerURL { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<Chapter> Chapters { get; set; }
        public List<string> TrackGenres { get; set; }
        public List<Artist> TrackArtists { get; set; }
        public ProtoTrack()
        {

        }
        public ProtoTrack(Track t)
        {
            _id = t._id;
            Album = new Album() { 
                _id = t.Album._id, 
                Name = t.Album.Name,
                DateAdded = DateTime.Now.ToUniversalTime(),
                Bio = "",
                TotalDiscs = 1,
                TotalTracks =  0,
                Publisher = "",
                ReleaseStatus = "",
                ReleaseType = "",
                ReleaseDate = DateTime.MinValue,
                AlbumArtists = new List<DbLink>(),
                AlbumArtPaths = new List<string>(),
                ContributingArtists = new List<DbLink>(),
                AlbumGenres = new List<string>(),
                AlbumArtCount = 0,
                AlbumArtDefault = 0,
                PlayCounts = new List<UserStat>(),
                Ratings = new List<UserStat>(),
                SkipCounts = new List<UserStat>(),
                ServerURL = ""
            };
            Position = t.Position;
            Disc = t.Disc;
            Format = t.Format;
            Bitrate = t.Bitrate;
            SampleRate = t.SampleRate;
            Channels = t.Channels;
            BitsPerSample = t.BitsPerSample;
            MusicBrainzID = t.MusicBrainzID;
            ISRC = t.ISRC;
            Year = t.Year;
            Name = t.Name;
            Duration = t.Duration;
            nextTrack = t.nextTrack;
            PlayCounts = t.PlayCounts;
            SkipCounts = t.SkipCounts;
            Ratings = t.Ratings;
            TrackArtCount = t.TrackArtCount;
            TrackArtDefault = t.TrackArtDefault;
            LyricsPath = t.LyricsPath;
            ServerURL = t.ServerURL;
            LastModified = t.LastModified;
            DateAdded = t.DateAdded;
            Path = t.Path;
            ReleaseDate = t.ReleaseDate;
            TrackGenres = t.TrackGenres;
            TrackArtists = t.TrackArtists.Select(x => new Artist() 
            { 
                _id = x._id, 
                Name = x.Name,
                Bio = "",
                Ratings = new List<UserStat>(),
                DateAdded = DateTime.Now.ToUniversalTime(),
                Genres = new List<string>(),
                ConnectedArtists = new List<DbLink>(),
                ArtistBannerArtCount = 0,
                ArtistPfpArtCount = 0,
                ArtistBannerArtDefault = 0,
                ArtistPfpDefault = 0,
                ArtistBannerPaths = new List<string>(),
                ArtistPfpPaths = new List<string>(),
                PlayCounts = new List<UserStat>(),
                SkipCounts = new List<UserStat>(),
                ServerURL = ""
            }).ToList();
        }
    }
    public class Track
    {
        public string _id { get; set; }
        public DbLink Album { get; set; }
        public int Position { get; set; }
        public int Disc { get; set; }
        public string Format { get; set; }
        public string Bitrate { get; set; }
        public string SampleRate { get; set; }
        public string Channels { get; set; }
        public string BitsPerSample { get; set; }
        public string MusicBrainzID { get; set; }
        public string ISRC { get; set; }
        public string Year { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Duration { get; set; }
        public string nextTrack { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public int TrackArtCount { get; set; }
        public int TrackArtDefault { get; set; }
        public string LyricsPath { get; set; }
        public string ServerURL { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<Chapter> Chapters { get; set; }
        public List<string> TrackGenres { get; set; }
        public List<DbLink> TrackArtists { get; set; }
        public Track()
        { 

        }
        public Track(ProtoTrack t)
        {
            _id = t._id;
            Album = new DbLink() { _id = t.Album._id, Name = t.Album.Name };
            Position = t.Position;
            Disc = t.Disc;
            Format = t.Format;
            Bitrate = t.Bitrate;
            SampleRate = t.SampleRate;
            Channels = t.Channels;
            BitsPerSample = t.BitsPerSample;
            MusicBrainzID = t.MusicBrainzID;
            ISRC = t.ISRC;
            Year = t.Year;
            Name = t.Name;
            Duration = t.Duration;
            nextTrack = t.nextTrack;
            PlayCounts = t.PlayCounts;
            SkipCounts = t.SkipCounts;
            Ratings = t.Ratings;
            TrackArtCount = t.TrackArtCount;
            TrackArtDefault = t.TrackArtDefault;
            LyricsPath = t.LyricsPath;
            ServerURL = t.ServerURL;
            LastModified = t.LastModified;
            DateAdded = t.DateAdded;
            Path = t.Path;
            ReleaseDate = t.ReleaseDate;
            TrackGenres = t.TrackGenres;
            Chapters = t.Chapters;
            TrackArtists = t.TrackArtists.Select(x=> new DbLink() { _id = x._id, Name = x.Name }).ToList();
        }
    }
    public class ResponseTrack
    {
        public string _id { get; set; }
        public DbLink Album { get; set; }
        public int Position { get; set; }
        public int Disc { get; set; }
        public string Format { get; set; }
        public string Bitrate { get; set; }
        public string SampleRate { get; set; }
        public string Channels { get; set; }
        public string BitsPerSample { get; set; }
        public string MusicBrainzID { get; set; }
        public string ISRC { get; set; }
        public string Year { get; set; }
        public string Name { get; set; }
        public string Duration { get; set; }
        public string nextTrack { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public int TrackArtCount { get; set; }
        public int TrackArtDefault { get; set; }
        public string ServerURL { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<Chapter> Chapters { get; set; }
        public List<string> TrackGenres { get; set; }
        public List<DbLink> TrackArtists { get; set; }
        public ResponseTrack()
        { 

        }
        public ResponseTrack(Track t)
        {
            _id = t._id;
            Album = t.Album;
            Position = t.Position;
            Disc = t.Disc;
            Format = t.Format;
            Bitrate = t.Bitrate;
            SampleRate = t.SampleRate;
            Channels = t.Channels;
            BitsPerSample = t.BitsPerSample;
            MusicBrainzID = t.MusicBrainzID;
            ISRC = t.ISRC;
            Year = t.Year;
            Name = t.Name;
            Duration = t.Duration;
            nextTrack = t.nextTrack;
            PlayCounts = t.PlayCounts;
            SkipCounts = t.SkipCounts;
            Ratings = t.Ratings;
            TrackArtCount = t.TrackArtCount;
            ServerURL = t.ServerURL;
            LastModified = t.LastModified;
            DateAdded = t.DateAdded;
            ReleaseDate = t.ReleaseDate;
            TrackGenres = t.TrackGenres;
            TrackArtists = t.TrackArtists;
            Chapters = t.Chapters;
        }
    }
}

