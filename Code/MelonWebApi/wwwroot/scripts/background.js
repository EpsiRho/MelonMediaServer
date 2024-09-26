// Global variables
var palette;
const colorThief = new ColorThief();

let cnvs = document.getElementById('bgcnvs'); // The canvas element
let cnvsctx = cnvs.getContext('2d'); // The canvas 2D context
cnvs.width = window.innerWidth;
cnvs.height = document.body.clientHeight * 2;

let currentPalette = [[0, 0, 0], [198, 198, 198]];
let targetPalette = [[0, 0, 0], [198, 198, 198]];
let transitionStartTime = null;
const transitionDuration = 75; // Duration of the transition in milliseconds
let paletteTransitionRequestId = null; // Stores the requestAnimationFrame ID
var RESOLUTION = 64; // Adjust as needed
var COLOR_SCALE = 255; // Adjust as needed

// Function to extract colors and initialize canvas
function extractColors(img, paletteSize) {
    function initializeCanvas() {
        //palette = colorThief.getPalette(img, paletteSize);
        palette = getColors(img, paletteSize);

        // Set the initial currentPalette
        currentPalette = palette;
        targetPalette = currentPalette;

        // Now that palettes are initialized, resize the canvas and draw
        resizeBackgroundCanvas();

        // Set up event listeners after initialization
        setupImageEventListeners();

        // Apply initial color overlay
        applyColorOverlay(1);
    }

    // Check if image is already loaded
    if (img.complete) {
        initializeCanvas();
    } else {
        img.addEventListener('load', initializeCanvas);
    }
}

function interpolateColor(color1, color2, t) {
    let r = Math.round(color1[0] + t * (color2[0] - color1[0]));
    let g = Math.round(color1[1] + t * (color2[1] - color1[1]));
    let b = Math.round(color1[2] + t * (color2[2] - color1[2]));
    return [r, g, b];
}

function startPaletteTransition() {
    transitionStartTime = null; // Reset the transition start time

    // Cancel any ongoing transition
    if (paletteTransitionRequestId !== null) {
        cancelAnimationFrame(paletteTransitionRequestId);
        paletteTransitionRequestId = null;
    }

    // Start a new transition
    paletteTransitionRequestId = requestAnimationFrame(stepPaletteTransition);
}

function stepPaletteTransition(timestamp) {
    if (!transitionStartTime) transitionStartTime = timestamp;
    let progress = (timestamp - transitionStartTime) / transitionDuration;
    if (progress > 1) progress = 1;

    // Interpolate colors
    let t = progress;
    applyColorOverlay(t);

    if (progress < 1) {
        paletteTransitionRequestId = requestAnimationFrame(stepPaletteTransition);
    } else {
        // Transition complete, set currentPalette to targetPalette
        currentPalette = targetPalette;
        transitionStartTime = null;
        paletteTransitionRequestId = null;
    }
}

let perlinImageData;
function drawPerlinNoise() {
    // Calculate the size of each block based on the RESOLUTION
    let blockWidth = cnvs.width / RESOLUTION;
    let blockHeight = cnvs.height / RESOLUTION;

    perlinImageData = cnvsctx.createImageData(cnvs.width, cnvs.height);
    let data = perlinImageData.data;

    for (let y = 0; y < RESOLUTION; y++) {
        for (let x = 0; x < RESOLUTION; x++) {
            // Calculate the noise value for the current block
            let noiseValue = perlin.get((x / RESOLUTION) * 2, (y / RESOLUTION) * 2);
            let v = (noiseValue + 1) / 2; // Normalize to 0-1 if necessary
            let gray = Math.round(v * 255);

            // Draw the block
            for (let by = 0; by < blockHeight; by++) {
                for (let bx = 0; bx < blockWidth; bx++) {
                    let pixelX = Math.floor(x * blockWidth + bx);
                    let pixelY = Math.floor(y * blockHeight + by);

                    // Ensure we don't go out of bounds due to rounding
                    if (pixelX >= cnvs.width || pixelY >= cnvs.height) continue;

                    let index = (pixelX + pixelY * cnvs.width) * 4;

                    data[index] = gray;     // R
                    data[index + 1] = gray; // G
                    data[index + 2] = gray; // B
                    data[index + 3] = 255;  // A
                }
            }
        }
    }

    // Put the generated image data onto the canvas
    cnvsctx.putImageData(perlinImageData, 0, 0);
}

//function drawPerlinNoise(cnvs) {
//    requestAnimationFrame(() => {
//        let pixel_width = cnvs.width / RESOLUTION;
//        let pixel_height = cnvs.height / RESOLUTION;

//        let overlap = 1;
//        for (let y = 0; y < cnvs.height; y += pixel_height) {
//            for (let x = 0; x < cnvs.width; x += pixel_width) {
//                let noiseValue = perlin.get(x / cnvs.width, y / cnvs.height); // Adjusted to use relative positions
//                let v = parseInt(noiseValue * COLOR_SCALE) / COLOR_SCALE;
//                let clr = lerpColors(palette[0], palette[1], v);
//                cnvsctx.fillStyle = clr;
//                cnvsctx.fillRect(x, y, pixel_width + overlap, pixel_height + overlap);
//            }
//        }
//    });
//}

function lerpColors(color1, color2, t) {
    let darkenFactor = 0.5; // Factor to darken the color by (0 to 1, where 1 is original color)

    let r = Math.round((color1[0] + t * (color2[0] - color1[0])) * darkenFactor);
    let g = Math.round((color1[1] + t * (color2[1] - color1[1])) * darkenFactor);
    let b = Math.round((color1[2] + t * (color2[2] - color1[2])) * darkenFactor);

    // Clamp values to the 0-255 range to ensure they are valid RGB values
    r = Math.max(0, Math.min(255, r));
    g = Math.max(0, Math.min(255, g));
    b = Math.max(0, Math.min(255, b));

    return [r, g, b];
}

function applyColorOverlay(t) {

    if (!currentPalette || !targetPalette) {
        console.error('Palettes are not initialized.');
        return;
    }

    if (!perlinImageData) {
        console.error('Perlin noise data is not available.');
        return;
    }

    // Create a new ImageData object for the colored image
    let imageData = cnvsctx.createImageData(cnvs.width, cnvs.height);
    let data = imageData.data;
    let perlinData = perlinImageData.data;

    let color1 = interpolateColor(currentPalette[0], targetPalette[0], t);
    let color2 = interpolateColor(currentPalette[1], targetPalette[1], t);

    for (let i = 0; i < data.length; i += 4) {
        let gray = perlinData[i]; // R channel from grayscale data
        let v = gray / 255;
        var darkenFactor = 1;


        let r = Math.round((color1[0] + v * (color2[0] - color1[0])) * darkenFactor);
        let g = Math.round((color1[1] + v * (color2[1] - color1[1])) * darkenFactor);
        let b = Math.round((color1[2] + v * (color2[2] - color1[2])) * darkenFactor);

        data[i] = r;
        data[i + 1] = g;
        data[i + 2] = b;
        data[i + 3] = 255; // Keep alpha channel fully opaque
    }

    // Draw the colored image onto the canvas
    cnvsctx.putImageData(imageData, 0, 0);
}


function resizeBackgroundCanvas() {
    // Ensure we're using the global cnvs and cnvsctx
    cnvs.width = window.innerWidth;
    cnvs.height = document.body.clientHeight * 1.01;
    console.log('client:');
    console.log(document.body.clientHeight);

    adjustResolution();

    // Redraw Perlin noise and reapply color overlay on resize
    drawPerlinNoise();
    applyColorOverlay(1);
    
}

function adjustResolution() {
    let maxDimension = Math.max(document.body.clientWidth, document.body.clientHeight);
    RESOLUTION = Math.max(128, maxDimension / 10); // Example adjustment, tweak as necessary
}

// Debounce function to limit function calls
function debounce(func, wait) {
    wait = 200;
    let timeout;
    return function () {
        clearTimeout(timeout);
        timeout = setTimeout(func, wait);
    };
}

// Function to set up image event listeners
function setupImageEventListeners() {
    var imgs = document.getElementsByClassName('item-image');

    const imagesArray = Array.from(imgs);
    for (let i = 0; i < imagesArray.length; i++) {
        imagesArray[i].addEventListener('click', debounce(function () {
            console.log('Changing bg');
            targetPalette = getColors(imagesArray[i], 2);
            startPaletteTransition();
        }));
        //imagesArray[i].addEventListener('mouseout', debounce(function () {
        //    console.log('Returning bg on mouseout');
        //    const img = document.getElementById('profile-pic');
        //    targetPalette = colorThief.getPalette(img, 2);
        //    startPaletteTransition();
        //}));
    }
}

function adjustCanvasHeight() {
    const canvas = document.getElementById('bgcnvs');
    const pageHeight = document.body.scrollHeight;

    // Set the canvas height
    canvas.height = pageHeight; // Adjust the internal canvas height for drawing

    // If you have a canvas width to adjust as well
    const pageWidth = document.body.scrollWidth;
    canvas.width = pageWidth;

    //console.log(canvas.height, canvas.width)
}

// Event listeners and initial setup
//window.addEventListener('load', function () {
//    console.log('Window loaded');

//    // Ensure cnvs and cnvsctx are initialized
//    cnvs = document.getElementById('bgcnvs');
//    cnvsctx = cnvs.getContext('2d');

//    // Start the initial color extraction
//    var img = document.getElementById('profile-pic');
//    extractColors(img, 2);

//    renderStats(true);
//});

// Event listener for window resize with debounce
setupImageEventListeners();
window.addEventListener('resize', debounce(resizeBackgroundCanvas, 200));
