function displayFavorites() {
    document.getElementById('favorite-track-artwork').src = favorites.track.artwork;
    document.getElementById('favorite-track-name').textContent = favorites.track.name;
    document.getElementById('favorite-track-artist').textContent = favorites.track.artist;

    document.getElementById('favorite-album-artwork').src = favorites.album.artwork;
    document.getElementById('favorite-album-name').textContent = favorites.album.name;
    document.getElementById('favorite-album-artist').textContent = favorites.album.artist;

    document.getElementById('favorite-artist-artwork').src = favorites.artist.artwork;
    document.getElementById('favorite-artist-name').textContent = favorites.artist.name;
}

// Call displayFavorites on page load
document.addEventListener('DOMContentLoaded', displayFavorites);