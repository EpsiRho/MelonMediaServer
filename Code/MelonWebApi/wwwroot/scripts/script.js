// User Info Variables
var userInfo = {
    username: 'johndoe',
    bio: 'Music lover and audiophile.',
    profilePicture: 'pfp.png',
    favoriteTrack: {
        name: 'Bohemian Rhapsody',
        artist: 'Queen',
        album: 'A Night at the Opera',
        artwork: 'defaultArtwork.png'
    },
    favoriteAlbum: {
        name: 'Abbey Road',
        artist: 'The Beatles',
        artwork: 'defaultArtwork.png'
    },
    favoriteArtist: {
        name: 'The Beatles',
        artwork: 'defaultArtwork.png'
    }
};

// Stats Configuration (Enable or Disable Stats)
var statsConfig = {
    listeningTimesEnabled: true,
    topTracksEnabled: true,
    topAlbumsEnabled: true,
    topArtistsEnabled: true,
    topGenresEnabled: true,
    recentTracksEnabled: true,
    recentAlbumsEnabled: true,
    recentArtistsEnabled: true
};

// Listening Times Data (Minutes listened per hour over the past month)
var listeningTimesData = [30, 20, 15, 10, 5, 2, 5, 10, 20, 50, 60, 80, 100, 120, 110, 90, 80, 70, 60, 50, 40, 35, 30, 25];

// Top Tracks (Add more items for demonstration)
var topTracks = [
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },

    {
        name: 'Track A',
        artist: 'Artist A',
        album: 'Album A',
        artwork: 'defaultArtwork.png'
    },
    // ... Add more tracks
];

// Top Albums (Add more items for demonstration)
var topAlbums = [
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album A',
        artist: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    // ... Add more albums
];

// Top Artists (Add more items for demonstration)
var topArtists = [
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist A',
        artwork: 'defaultArtwork.png'
    },
    // ... Add more artists
];

// Top Genres (Add more items for demonstration)
var topGenres = ['Rock', 'Pop', 'Jazz', 'Classical', 'Hip-Hop', 'Electronic', 'Country'];

// Recent Tracks (Add more items for demonstration)
var recentTracks = [
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Track X',
        artist: 'Artist X',
        album: 'Album X',
        artwork: 'defaultArtwork.png'
    },
    // ... Add more tracks
];

// Recent Albums (Add more items for demonstration)
var recentAlbums = [
    {
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Album X',
        artist: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    // ... Add more albums
];

// Recent Artists (Add more items for demonstration)
var recentArtists = [
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },{
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    {
        name: 'Artist X',
        artwork: 'defaultArtwork.png'
    },
    // ... Add more artists
];
// Function to update user info
function updateUserInfo() {
    document.getElementById('username').textContent = userInfo.username;
    document.getElementById('bio').textContent = userInfo.bio;
    document.getElementById('profile-pic').src = userInfo.profilePicture;

    // Favorite Track
    document.getElementById('favorite-track-art').src = userInfo.favoriteTrack.artwork;
    document.getElementById('favorite-track').textContent = userInfo.favoriteTrack.name;
    document.getElementById('favorite-track-artist').textContent = userInfo.favoriteTrack.artist;

    // Favorite Album
    document.getElementById('favorite-album-art').src = userInfo.favoriteAlbum.artwork;
    document.getElementById('favorite-album').textContent = userInfo.favoriteAlbum.name;
    document.getElementById('favorite-album-artist').textContent = userInfo.favoriteAlbum.artist;

    // Favorite Artist
    document.getElementById('favorite-artist-art').src = userInfo.favoriteArtist.artwork;
    document.getElementById('favorite-artist').textContent = userInfo.favoriteArtist.name;
}

// Function to render stats
function renderStats() {
    // Listening Times
    if (statsConfig.listeningTimesEnabled) {
        renderListeningChart();
        window.addEventListener('resize', renderListeningChart);
    } else {
        document.getElementById('listening-times').style.display = 'none';
    }

    // Render item lists with limited items
    //renderItemList('top-tracks', topTracks, statsConfig.topTracksEnabled, 'track');
    //renderItemList('top-albums', topAlbums, statsConfig.topAlbumsEnabled, 'album');
    //renderItemList('top-artists', topArtists, statsConfig.topArtistsEnabled, 'artist');
    //renderItemList('top-genres', topGenres, statsConfig.topGenresEnabled, 'genre');
    //renderItemList('recent-tracks', recentTracks, statsConfig.recentTracksEnabled, 'track');
    //renderItemList('recent-albums', recentAlbums, statsConfig.recentAlbumsEnabled, 'album');
    //renderItemList('recent-artists', recentArtists, statsConfig.recentArtistsEnabled, 'artist');
    const sections = ['top-tracks', 'top-albums', 'top-artists', 'top-genres', 'recent-tracks', 'recent-albums', 'recent-artists'];
    sections.forEach(sectionId => {
        const isEnabled = statsConfig[sectionId.replace('-', '') + 'Enabled'];
        const timeRange = document.getElementById(sectionId + '-selector').value;
        updateWidgetData(sectionId, timeRange);
    });
}

// Function to render the listening chart
function renderListeningChart() {
    var chartContainer = document.getElementById('listening-chart');
    chartContainer.innerHTML = '';
    var chartWidth = chartContainer.clientWidth;
    var barWidth = chartWidth / listeningTimesData.length;
    var maxTime = Math.max(...listeningTimesData);
    for (var i = 0; i < listeningTimesData.length; i++) {
        var bar = document.createElement('div');
        var barHeight = (listeningTimesData[i] / maxTime) * 100;
        bar.className = 'bar';
        bar.style.left = (i * barWidth) + 'px';
        bar.style.width = (barWidth - 2) + 'px'; // Subtract 2 for spacing
        bar.style.height = barHeight + '%';
        bar.title = i + ':00 - ' + listeningTimesData[i] + ' minutes';
        bar.innerHTML = '<span>' + listeningTimesData[i] + '</span>';
        chartContainer.appendChild(bar);
    }
}

// Function to render item lists with show more functionality using modal
function renderItemList(sectionId, items, isEnabled, itemType) {
    if (isEnabled) {
        console.log(items);
        var listContainer = document.getElementById(sectionId + '-list');
        listContainer.innerHTML = '';
        var maxItemsToShow = listContainer.getBoundingClientRect().width / 49;
        const entries = Object.entries(items);

        var slicedEntries = entries.slice(0, maxItemsToShow);
        var itemsToShow = Object.fromEntries(slicedEntries);

        console.log(itemsToShow.length);
        const itemsArray = Object.values(itemsToShow);
        for (let i = 0; i < itemsArray.length; i++) {
            var item = itemsArray[i];
            console.log(item);
            var itemElement = createItemElement(item, itemType);
            listContainer.appendChild(itemElement);
        }

        var showMoreButton = document.querySelector('button[data-target="' + sectionId + '-list"]');
        showMoreButton.style.display = items.length > maxItemsToShow ? 'block' : 'none';
        setupShowMoreModal(sectionId, items, itemType);
    } else {
        document.getElementById(sectionId).style.display = 'none';
    }
}

// Function to create item elements
function createItemElement(item, itemType) {
    var itemDiv = document.createElement('div');
    itemDiv.className = 'item';
    if (itemType === 'genre') {
        itemDiv.classList.add('genre');
        var infoDiv = document.createElement('div');
        infoDiv.className = 'item-info';
        var nameP = document.createElement('p');
        nameP.textContent = item;
        infoDiv.appendChild(nameP);
    }
    if (itemType !== 'genre') {
        console.log(item);
        var img = document.createElement('img');
        img.src = item.artwork || 'defaultArtwork.png';
        img.alt = item.name;
        itemDiv.appendChild(img);
        var infoDiv = document.createElement('div');
        infoDiv.className = 'item-info';
        var nameP = document.createElement('p');
        nameP.textContent = item.name;
        infoDiv.appendChild(nameP);

        if (itemType === 'track' || itemType === 'album') {
            var artistP = document.createElement('p');
            artistP.textContent = item.artist;
            infoDiv.appendChild(artistP);
        }
    }

    itemDiv.appendChild(infoDiv);
    return itemDiv;
}

// Function to setup Show More modal
function setupShowMoreModal(sectionId, items, itemType) {
    var showMoreButton = document.querySelector('button[data-target="' + sectionId + '-list"]');
    var modal = document.getElementById('modal');
    var modalTitle = document.getElementById('modal-title');
    var modalBody = document.getElementById('modal-body');
    var modalClose = document.getElementById('modal-close');

    showMoreButton.addEventListener('click', function() {
        modal.style.display = 'block';
        modalTitle.textContent = document.querySelector('#' + sectionId + ' h2').textContent;
        modalBody.innerHTML = '';
        modalBody.scrollTop = 0;

        items.forEach(function(item) {
            var itemElement = createItemElement(item, itemType);
            modalBody.appendChild(itemElement);
        });
    });

    modalClose.addEventListener('click', function() {
        modal.style.display = 'none';
    });

    // Close modal when clicking outside of modal content
    window.onclick = function(event) {
        if (event.target == modal) {
            modal.style.display = 'none';
        }
    };
}

// Check system preference and apply dark mode if preferred
// Function to apply the correct theme on page load
function applyTheme(theme) {
    if (theme === 'dark') {
        document.body.classList.add('dark-mode');
    } else {
        document.body.classList.remove('dark-mode');
    }
}

// Check system preference and apply the theme if no user preference exists
function applyPreferredColorScheme() {
    const userPrefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const savedTheme = localStorage.getItem('theme');

    // Apply the saved theme if it exists, otherwise use system preference
    if (savedTheme) {
        applyTheme(savedTheme);
    } else {
        applyTheme(userPrefersDark ? 'dark' : 'light');
    }
}

function setupDataSelectors() {
    const selectors = document.querySelectorAll('.data-selector');
    selectors.forEach(selector => {
        selector.addEventListener('change', function () {
            const sectionId = this.id.replace('-selector', '');
            console.log('beep');
            updateWidgetData(sectionId, this.value);
        });
    });
}

async function updateWidgetData(sectionId, timeRange) {
    // Fetch or filter data based on timeRange
    // For demonstration, we'll use dummy data
    const dummyData = await getNewData(sectionId, timeRange);
    if (sectionId === 'listening-times') {
        // Update Listening Times Chart
        updateListeningChart(dummyData);
    } else {
        // Update Item Lists
        const itemType = sectionId.includes('tracks') ? 'track' :
                         sectionId.includes('albums') ? 'album' :
                sectionId.includes('artists') ? 'artist' : 'genre';
        console.log(itemType);
        renderItemList(sectionId, dummyData, true, itemType);
    }
}

async function getNewData(sectionId, timeRange) {
    console.log(timeRange);
    console.log(sectionId);

    if (sectionId === 'top-tracks')
    {
        var tracks = await MelonCall(0, 100, 'user-page/top-tracks');
        console.log('freedom');
        return tracks;W
    }
    else if (sectionId === 'top-albums')
    {
        var albums = await MelonCall(0, 100, 'user-page/top-albums');
        return albums;
    }
    else if (sectionId === 'top-artists') {
        var artists = await MelonCall(0, 100, 'user-page/top-artists');
        return artists;
    }

    // Return different data based on sectionId and timeRange
    // This should be replaced with actual data fetching logic
    //renderItemList('top-tracks', topTracks, statsConfig.topTracksEnabled, 'track');
    //renderItemList('top-albums', topAlbums, statsConfig.topAlbumsEnabled, 'album');
    //renderItemList('top-artists', topArtists, statsConfig.topArtistsEnabled, 'artist');
    //renderItemList('top-genres', topGenres, statsConfig.topGenresEnabled, 'genre');
    //renderItemList('recent-tracks', recentTracks, statsConfig.recentTracksEnabled, 'track');
    //renderItemList('recent-albums', recentAlbums, statsConfig.recentAlbumsEnabled, 'album');
    //renderItemList('recent-artists', recentArtists, statsConfig.recentArtistsEnabled, 'artist');
    return [];
}

function MelonCall(page, count, api){
    const userId = '66469992586a37512366abc6';
    const jwtToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IkVwc2kiLCJyb2xlIjoiQWRtaW4iLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3VzZXJkYXRhIjoiNjY0Njk5OTI1ODZhMzc1MTIzNjZhYmM2IiwibmJmIjoxNzI3MTYzMjEzLCJleHAiOjE3MjcxNjY4MTMsImlhdCI6MTcyNzE2MzIxM30.pK1BlAlMF18RSm92bj2qphK2XhRRYN_KLOzE2ijfpZg';  // Replace this with your actual JWT token

    const apiUrl = `${api}?userId=${userId}&page=${page}&count=${count}`;

    return fetch(apiUrl, {
        method: 'GET',
        headers: {
            'Authorization': `Bearer ${jwtToken}`,
            'Content-Type': 'application/json',
        },
    })
    .then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok ' + response.statusText);
        }
        return response.json();
    })
    .catch(error => {
        console.error('There was a problem with the fetch operation:', error);
    });
}


// Toggle dark mode manually
document.getElementById('dark-mode-toggle').addEventListener('click', function () {
    const currentTheme = document.body.classList.contains('dark-mode') ? 'dark' : 'light';
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    console.log(newTheme);
    // Apply the new theme
    applyTheme(newTheme);

    // Save user preference in localStorage
    localStorage.setItem('theme', newTheme);
});

// Apply the correct theme when the page loads
window.addEventListener('DOMContentLoaded', function () {
    applyPreferredColorScheme();
});


// Initialize the page
updateUserInfo();
renderStats();
setupDataSelectors();


function resizeCanvas() {
    renderStats();
}

// Event listener for window resize
window.addEventListener('resize', resizeCanvas);

// Call resizeCanvas on page load to ensure the canvas is the correct size
resizeCanvas();

