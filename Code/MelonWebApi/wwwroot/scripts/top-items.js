// Top Tracks / Albums / Artists //
function populateCarousel(carouselId, items) {
    const carousel = document.getElementById(carouselId).querySelector('.carousel');
    items.forEach(item => {
        const itemDiv = document.createElement('div');
        itemDiv.className = 'carousel-item';
        if(item.name === undefined){
            itemDiv.innerHTML = `
                <img src="${item.artwork}" alt="${item.name}">
                <p>${item.artist}</p>
            `;
        }
        else{
            itemDiv.innerHTML = `
                <img src="${item.artwork}" alt="${item.name}">
                <p>${item.name} - ${item.artist}</p>
            `;
        }
        carousel.appendChild(itemDiv);
    });
}
document.addEventListener('DOMContentLoaded', function() {
    var carousels = document.querySelectorAll('.carousel-container');

    carousels.forEach(carousel => {
        carousel.addEventListener('wheel', function(e) {
            e.preventDefault(); // Prevent vertical scroll
            var delta = Math.sign(e.deltaY); // Determine the scroll direction
            carousel.scrollLeft += delta *  100; // Scroll horizontally
        }, { passive: false });
    });
});

// Populate each carousel on document load
document.addEventListener('DOMContentLoaded', () => {
    populateCarousel('top-tracks', topTracks);
    // Repeat for top albums and artists with their respective data
});

document.addEventListener('DOMContentLoaded', () => {
    populateCarousel('top-albums', topAlbums);
    // Repeat for top albums and artists with their respective data
});

document.addEventListener('DOMContentLoaded', () => {
    populateCarousel('top-artists', topArtists);
    // Repeat for top albums and artists with their respective data
});