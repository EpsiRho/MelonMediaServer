document.addEventListener('DOMContentLoaded', function() {
    // This would be replaced with an actual API call
    fetch('favorites.json')
      .then(response => response.json())
      .then(data => {
        document.querySelector('.favorite-track p').textContent = data.track;
        document.querySelector('.favorite-track .artist').textContent = data.trackArtist;
        document.querySelector('.favorite-album p').textContent = data.album;
        document.querySelector('.favorite-album .artist').textContent = data.albumArtist;
        document.querySelector('.favorite-artist p').textContent = data.artist;
      });
  });