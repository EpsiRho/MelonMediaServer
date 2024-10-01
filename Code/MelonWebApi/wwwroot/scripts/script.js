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

// Data variables (initialized as empty arrays)
var topTracks = [];
var topAlbums = [];
var topArtists = [];
var topGenres = [];
var recentTracks = [];
var recentAlbums = [];
var recentArtists = [];
var listeningTimeStats = [];

// Function to update user info
function updateUserInfo() {
    async function initializeCanvas() {
        //console.log('setting canvas');
        //extractColors(img, 2);

        //console.log(currentPalette);

        //drawPerlinNoise();

        // Set up event listeners after initialization
        //setupImageEventListeners();

        // Apply initial color overlay
        //applyColorOverlay(1);

        //resizeBackgroundCanvas();

        //WhatTheFuck();

    }

    var img = document.getElementById('profile-pic');

    // Check if image is already loaded
    img.addEventListener('load', initializeCanvas);

    document.getElementById('username').textContent = userInfo.username;
    document.getElementById('bio').textContent = userInfo.bio;
    document.getElementById('profile-pic').src = userInfo.profilePicture;
    document.getElementById('tab-title').title = `${userInfo.username}'s Listening Stats`;

    // Favorite Track
    document.getElementById('favorite-track-art').src = userInfo.favoriteTrack.artwork || 'defaultArtwork.png';
    document.getElementById('favorite-track').textContent = userInfo.favoriteTrack.name || 'Track Name';
    document.getElementById('favorite-track-artist').textContent = userInfo.favoriteTrack.artist || 'Artist Name';

    // Favorite Album
    document.getElementById('favorite-album-art').src = userInfo.favoriteAlbum.artwork || 'defaultArtwork.png';
    document.getElementById('favorite-album').textContent = userInfo.favoriteAlbum.name || 'Album Name';
    document.getElementById('favorite-album-artist').textContent = userInfo.favoriteAlbum.artist || 'Artist Name';

    // Favorite Artist
    document.getElementById('favorite-artist-art').src = userInfo.favoriteArtist.artwork || 'defaultArtwork.png';
    document.getElementById('favorite-artist').textContent = userInfo.favoriteArtist.name || 'Artist Name';
}



// Variables for caching
var previousMaxItemsToShow = {};
var itemElementsCache = {};

// Function to render stats
async function renderStats(useNew) {
    // Listening Times
    if (statsConfig.listeningTimesEnabled) {
        if (useNew) {
            var ltTimeRangeSelector = document.getElementById('listening-times-selector');
            var ltTimeRange = ltTimeRangeSelector ? ltTimeRangeSelector.value : 'month';

            var apiUrl = `user-page/listening-time?userId=${userId}&timeRange=${ltTimeRange}`;

            listeningTimeStats = await fetch(apiUrl, {
                method: 'GET',
                headers: {
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
                    return [];
                });
        renderListeningChart(listeningTimeStats);
        }
    } else {
        document.getElementById('listening-times').style.display = 'none';
    }

    // Render item lists with limited items
    const sections = ['top-tracks', 'top-albums', 'top-artists', 'top-genres', 'recent-tracks', 'recent-albums', 'recent-artists'];
    sections.forEach(sectionId => {
        const isEnabled = statsConfig[sectionId.replace('-', '') + 'Enabled'];
        const timeRangeSelector = document.getElementById(sectionId + '-selector');
        const timeRange = timeRangeSelector ? timeRangeSelector.value : 'month'; // Default to 'month' if no selector
        updateWidgetData(sectionId, timeRange, useNew);
    });

}

// Function to render the listening chart
var myChart = null;
function renderListeningChart(minutesListened) {
    const ctx = document.getElementById('myChart')?.getContext('2d');
    if (!ctx) return; // Exit if the chart element doesn't exist

    // Hour labels for the x-axis
    const labels = [
        '1am', '2am', '3am', '4am', '5am', '6am',
        '7am', '8am', '9am', '10am', '11am', '12pm',
        '1pm', '2pm', '3pm', '4pm', '5pm', '6pm',
        '7pm', '8pm', '9pm', '10pm', '11pm', '12am'
    ];

    // Configuration for the Chart.js line chart
    const data = {
        labels: labels,
        datasets: [{
            label: 'Minutes Listened',
            data: minutesListened,
            fill: true,
            backgroundColor: 'rgba(75,192,192,0.2)',
            borderColor: '#4bc0c0',
            pointBackgroundColor: '#4bc0c0',
            tension: 0.4
        }]
    };

    const options = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                display: false
            },
            tooltip: {
                mode: 'index',
                intersect: false,
            }
        },
        scales: {
            x: {
                grid: {
                    display: false
                },
                ticks: {
                    autoSkip: false,
                    maxRotation: 45,
                    minRotation: 45
                }
            },
            y: {
                beginAtZero: true,
                max: Math.max(...minutesListened) + 10 || 100,
                ticks: {
                    stepSize: 10
                }
            }
        }
    };

    // Create or update the Chart.js line chart
    if (myChart) {
        myChart.options = options;
        myChart.data = data;
        myChart.update();
    } else {
        myChart = new Chart(ctx, {
            type: 'line',
            data: data,
            options: options
        });
    }
}

// Function to render item lists with show more functionality using modal
function renderItemList(sectionId, items, isEnabled, itemType) {
    if (!isEnabled) {
        document.getElementById(sectionId).style.display = 'none';
        return;
    }

    var listContainer = document.getElementById(sectionId + '-list');

    // Calculate maxItemsToShow based on container width and item width
    var listContainer = document.getElementById(sectionId + '-list');
    var itemContainer = listContainer.getElementsByClassName('item');
    var maxItemsToShow = 0;

    if (itemContainer.length > 0) {
        maxItemsToShow = listContainer.getBoundingClientRect().width / (itemContainer[0].clientWidth / 2);
    } else {
        maxItemsToShow = listContainer.getBoundingClientRect().width / 67;
    }

    maxItemsToShow = Math.floor(maxItemsToShow);



    // Check if maxItemsToShow has changed
    if (previousMaxItemsToShow[sectionId] === maxItemsToShow && !itemElementsCache[sectionId] === null) {
        // No change, do nothing
        return;
    }
    previousMaxItemsToShow[sectionId] = maxItemsToShow;

    if (!itemElementsCache[sectionId] || itemElementsCache[sectionId] === null) {
        itemElementsCache[sectionId] = { elements: [], needsUpdate: false };
        // Create all item elements and cache them
        var itemsArray = itemType !== 'genre' ? items : items; // For genres, items is already an array

        if (itemType !== 'genre') {
            itemsArray = Object.values(items);
        }
        else {
            itemsArray = Object.keys(items);
        }

        // Clear the container and append all items once
        listContainer.innerHTML = '';
        for (let i = 0; i < itemsArray.length; i++) {
            var item = itemsArray[i];
            var itemElement = createItemElement(item, itemType);
            itemElementsCache[sectionId].elements.push(itemElement);
            listContainer.appendChild(itemElement);
        }
    }

    // Now, show or hide items based on maxItemsToShow
    for (let i = 0; i < itemElementsCache[sectionId].elements.length; i++) {
        var itemElement = itemElementsCache[sectionId].elements[i];
        if (i < maxItemsToShow) {
            itemElement.style.display = ''; // show
        } else {
            itemElement.style.display = 'none'; // hide
        }
    }

    var showMoreButton = document.querySelector('button[data-target="' + sectionId + '-list"]');
    showMoreButton.style.display = itemElementsCache[sectionId].elements.length > maxItemsToShow ? 'block' : 'none';

    setupShowMoreModal(sectionId, items, itemType);

    debounce(FixBg, 200);
}


// Function to create item elements
function createItemElement(item, itemType) {
    var itemDiv = document.createElement('div');
    itemDiv.className = 'item';
    var infoDiv = document.createElement('div');
    infoDiv.className = 'item-info';

    if (itemType === 'genre') {
        itemDiv.classList.add('genre');
        var nameP = document.createElement('p');
        nameP.textContent = item;
        infoDiv.appendChild(nameP);
    } else {
        var img = document.createElement('img');
        img.src = item.artwork || 'defaultArtwork.png';
        img.alt = item.name;
        img.addEventListener('click', function () {
            console.log('Changing bg');
            targetPalette = getColors(img, 2);
            startPaletteTransition();
        });
        img.classList.add('item-image')
        itemDiv.appendChild(img);

        var nameP = document.createElement('p');
        nameP.className = "item-info-name";
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

// Initialize modal event listeners once
var modalInitialized = false;

function initializeModal() {
    if (modalInitialized) return;
    modalInitialized = true;

    var modal = document.getElementById('modal');
    var modalClose = document.getElementById('modal-close');

    modalClose.addEventListener('click', function () {
        modal.style.display = 'none';
        document.body.classList.remove('modal-open');
    });

    // Close modal when clicking outside of modal content
    window.addEventListener('click', function (event) {
        if (event.target == modal) {
            modal.style.display = 'none';
            document.body.classList.remove('modal-open');
        }
    });
}

var showMoreButtonsInitialized = {};

function setupShowMoreModal(sectionId, items, itemType) {
    initializeModal(); // Ensure modal event listeners are set

    var showMoreButton = document.querySelector('button[data-target="' + sectionId + '-list"]');

    // Check if the listener has already been added
    if (showMoreButtonsInitialized[sectionId]) return;
    showMoreButtonsInitialized[sectionId] = true;

    showMoreButton.addEventListener('click', function () {
        var modal = document.getElementById('modal');
        var modalTitle = document.getElementById('modal-title');
        var modalBody = document.getElementById('modal-body');

        modal.style.display = 'block';
        document.body.classList.add('modal-open');
        modalTitle.textContent = document.querySelector('#' + sectionId + ' h2').textContent;
        modalBody.innerHTML = '';
        modalBody.scrollTop = 0;

        if (itemType !== 'genre') {
            items.forEach(async function (item) {
                var itemElement = createItemElement(item, itemType);
                modalBody.appendChild(itemElement);
            });
        }
        else {
            const entries = Object.keys(items);
            entries.forEach(async function (item) {
                var itemElement = createItemElement(item, itemType);
                modalBody.appendChild(itemElement);
            });
        }
    });
}

// Apply the correct theme on page load
function applyTheme(theme) {
    if (theme === 'dark') {
        document.body.classList.add('dark-mode');
    } else {
        document.body.classList.remove('dark-mode');
    }
}

function applyPreferredColorScheme() {
    const userPrefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const savedTheme = localStorage.getItem('theme');

    if (savedTheme) {
        applyTheme(savedTheme);
    } else {
        applyTheme(userPrefersDark ? 'dark' : 'light');
    }
}

async function setupDataSelectors() {
    const selectors = document.querySelectorAll('.data-selector');
    selectors.forEach(selector => {
        selector.addEventListener('change', async function () {
            var ltTimeRangeSelector = document.getElementById('listening-times-selector');
            var ltTimeRange = ltTimeRangeSelector ? ltTimeRangeSelector.value : 'month';

            var apiUrl = `user-page/listening-time?userId=${userId}&timeRange=${ltTimeRange}`;

            listeningTimeStats = await fetch(apiUrl, {
                method: 'GET',
                headers: {
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
                    return [];
                });
            renderListeningChart(listeningTimeStats);

            const sectionId = this.id.replace('-selector', '');
            updateWidgetData(sectionId, this.value, true);
        });
    });
}

async function updateWidgetData(sectionId, timeRange, getNew) {
    var data = [];
    if (getNew) {
        //console.log("Get Data");
        data = await getNewData(sectionId, timeRange);
    } else {
        data = getCachedData(sectionId);
    }

    if (sectionId === 'listening-times') {
        
    } else {
        const itemType = sectionId.includes('tracks') ? 'track' :
            sectionId.includes('albums') ? 'album' :
                sectionId.includes('artists') ? 'artist' : 'genre';
        renderItemList(sectionId, data, true, itemType);
    }
}

function getCachedData(sectionId) {
    if (sectionId === 'top-tracks') return topTracks;
    if (sectionId === 'top-albums') return topAlbums;
    if (sectionId === 'top-artists') return topArtists;
    if (sectionId === 'top-genres') return topGenres;
    if (sectionId === 'recent-tracks') return recentTracks;
    if (sectionId === 'recent-albums') return recentAlbums;
    if (sectionId === 'recent-artists') return recentArtists;

    return [];
}

async function getNewData(sectionId, timeRange) {
    // Use MelonCall function to fetch data
    let data = [];
    try {
        if (sectionId === 'top-tracks') {
            data = await MelonCall(0, 100, 'user-page/top-tracks', timeRange);
            topTracks = data;
        } else if (sectionId === 'top-albums') {
            data = await MelonCall(0, 100, 'user-page/top-albums', timeRange);
            topAlbums = data;
        } else if (sectionId === 'top-artists') {
            data = await MelonCall(0, 100, 'user-page/top-artists', timeRange);
            topArtists = data;
        } else if (sectionId === 'top-genres') {
            data = await MelonCall(0, 100, 'user-page/top-genres', timeRange);
            topGenres = data;
        } else if (sectionId === 'recent-tracks') {
            data = await MelonCall(0, 100, 'user-page/recent-tracks', timeRange);
            recentTracks = data;
        } else if (sectionId === 'recent-albums') {
            data = await MelonCall(0, 100, 'user-page/recent-albums', timeRange);
            recentAlbums = data;
        } else if (sectionId === 'recent-artists') {
            data = await MelonCall(0, 100, 'user-page/recent-artists', timeRange);
            recentArtists = data;
        }

        // Invalidate cache
        itemElementsCache[sectionId] = null;
    } catch (error) {
        console.error('Error fetching data for ' + sectionId + ':', error);
    }
    return data;
}

// MelonCall function
function MelonCall(page, count, api, timeRange) {
    //console.log(timeRange);
    const apiUrl = `${api}?userId=${userId}&page=${page}&count=${count}&timeRange=${timeRange}`;

    return fetch(apiUrl, {
        method: 'GET',
        headers: {
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
            return [];
        });
}

// Toggle dark mode manually
document.getElementById('dark-mode-toggle').addEventListener('click', function () {
    const currentTheme = document.body.classList.contains('dark-mode') ? 'dark' : 'light';
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    applyTheme(newTheme);
    localStorage.setItem('theme', newTheme);
});

// Apply the correct theme when the page loads
window.addEventListener('DOMContentLoaded', function () {
    applyPreferredColorScheme();
});

// Initialize the page
//updateUserInfo();
//renderStats(true);
//setupDataSelectors();

// Debounced resize event handler


// Event listeners and initial setup
window.addEventListener('load', async function () {
    console.log('loaded');
    updateUserInfo();
    renderStats(true);
    setupDataSelectors();
    setupImageEventListeners();
    const resizeObserver = new ResizeObserver(debounce(function () {

        console.group();
       // console.log(`body size changed: ${entries[0].target.clientHeight}, ${entries[0].target.clientWidth}`);
        console.log(`re-rendering stat widgets`);
        renderStats(false);

        console.groupEnd();

        FixBg();
        cnvs.style.display = `block`;

        console.groupEnd();
    }), 15);

    // start observing a DOM node
    resizeObserver.observe(document.body)
    FixBg();


    console.log(document.body.clientHeight);

});

//window.addEventListener('resize', debounce(resizeCanvas, 50));

