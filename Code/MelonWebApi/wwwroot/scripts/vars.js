const userProfile = {
    username: "Epsi",
    profilePicture: "16 05-06-20 - square.png",
    bio: "Music lover, live show enthusiast."
};

const listeningTimeStats = [
    {"12:00am-1:00am": 60},
    {"1:00am-2:00am": 90},
    {"2:00am-3:00am": 30},
    {"3:00am-4:00am": 290},
    {"4:00am-5:00am": 90},
    {"5:00am-6:00am": 90},
    {"6:00am-7:00am": 90},
    {"7:00am-8:00am": 190},
    {"8:00am-9:00am": 90},
    {"9:00am-10:00am": 40},
    {"10:00am-11:00am": 90},
    {"11:00am-12:00pm": 90},
    {"12:00pm-1:00pm": 20},
    {"1:00pm-2:00pm": 10},
    {"2:00pm-3:00pm": 90},
    {"3:00pm-4:00pm": 20},
    {"4:00pm-5:00pm": 40},
    {"5:00pm-6:00pm": 90},
    {"6:00pm-7:00pm": 50},
    {"7:00pm-8:00pm": 70},
    {"8:00pm-9:00pm": 30},
    {"9:00pm-10:00pm": 190},
    {"10:00pm-11:00pm": 110},
    {"11:00pm-12:00am": 90},
];

const listeningTimePerPeriod = {
    day: 120,
    week: 840,
    month: 3600,
    year: 43800
};


const topTracks = [
    { name: "Track 1", artist: "Artist 1", artwork: "defaultArtwork.png" },
    { name: "Track 2", artist: "Artist 2", artwork: "defaultArtwork.png" },
    { name: "Track 3", artist: "Artist 3", artwork: "defaultArtwork.png" },
    { name: "Track 4", artist: "Artist 4", artwork: "defaultArtwork.png" },
    { name: "Track 5", artist: "Artist 5", artwork: "defaultArtwork.png" },
    // Add up to 10 items...
];

const topAlbums = [
    { name: "Album 1", artist: "Artist 1", artwork: "defaultArtwork.png" },
    { name: "Album 2", artist: "Artist 2", artwork: "defaultArtwork.png" },
    { name: "Album 3", artist: "Artist 3", artwork: "defaultArtwork.png" },
    { name: "Album 4", artist: "Artist 4", artwork: "defaultArtwork.png" },
    { name: "Album 5", artist: "Artist 5", artwork: "defaultArtwork.png" },
    // Add up to 10 items...
];

const topArtists = [
    { artist: "Artist 1", artwork: "defaultArtwork.png" },
    { artist: "Artist 2", artwork: "defaultArtwork.png" },
    { artist: "Artist 3", artwork: "defaultArtwork.png" },
    { artist: "Artist 4", artwork: "defaultArtwork.png" },
    { artist: "Artist 5", artwork: "defaultArtwork.png" },
    // Add up to 10 items...
];

// Example favorites data
const favorites = {
    track: {
        name: "Heavy With Hoping",
        artist: "Madeon",
        artwork: "defaultArtwork.png"
    },
    album: {
        name: "Good Faith",
        artist: "Madeon",
        artwork: "defaultArtwork.png"
    },
    artist: {
        name: "Madeon",
        artwork: "defaultArtwork.png"
    }
};