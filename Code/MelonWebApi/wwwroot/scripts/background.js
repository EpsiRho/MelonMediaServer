const RESOLUTION =  150;
const COLOR_SCALE =  250;
let palette;

let cnvs = document.getElementById('bgcnvs');
cnvs.width = window.innerWidth;
cnvs.height = document.documentElement.scrollHeight;
let ctx = cnvs.getContext('2d');


const img = document.getElementById('profile-pic');
palette = extractColors(img, 2); // Extract a palette of 5 colors
console.log(palette);

document.body.style.background = lerpColors(palette[0], palette[0], 0.5);




function lerpColors(color1, color2, t) {
    let darkenFactor = 0.5; // Factor to darken the color by (0 to 1, where 1 is original color)

    let r = Math.round((color1[0] + t * (color2[0] - color1[0])) * darkenFactor);
    let g = Math.round((color1[1] + t * (color2[1] - color1[1])) * darkenFactor);
    let b = Math.round((color1[2] + t * (color2[2] - color1[2])) * darkenFactor);

    // Clamp values to the 0-255 range to ensure they are valid RGB values
    r = Math.max(0, Math.min(255, r));
    g = Math.max(0, Math.min(255, g));
    b = Math.max(0, Math.min(255, b));

    return "#" + r.toString(16).padStart(2, '0') + g.toString(16).padStart(2, '0') + b.toString(16).padStart(2, '0');
}

function extractColors(img, paletteSize) {
    const colorThief = new ColorThief();

    if (img.complete) {
        console.log('complete');
        return colorThief.getPalette(img, paletteSize);
      } else {
        console.log('still loading');
        img.addEventListener('load', function() {
            console.log('complete');
            img = document.getElementById('profile-pic');
            colorThief.getPalette(img, paletteSize);
            let cnvs = document.getElementById('bgcnvs');
            cnvs.width = window.innerWidth;
            cnvs.height = document.documentElement.scrollHeight;
            let ctx = cnvs.getContext('2d');


            const img = document.getElementById('profile-pic');
            palette = extractColors(img, 2); // Extract a palette of 5 colors
            console.log(palette);

            document.body.style.background = lerpColors(palette[0], palette[0], 0.5);
        });
      }
}

function drawPerlinNoise(cnvs) {
    requestAnimationFrame(() => {
        let pixel_width = cnvs.width / RESOLUTION;
        let pixel_height = cnvs.height / RESOLUTION;

        let overlap = 1;
        for (let y = 0; y < cnvs.height; y += pixel_height) {
            for (let x = 0; x < cnvs.width; x += pixel_width) {
                let noiseValue = perlin.get(x / cnvs.width, y / cnvs.height); // Adjusted to use relative positions
                let v = parseInt(noiseValue * COLOR_SCALE) / COLOR_SCALE;
                let clr = lerpColors(palette[0], palette[1],  v);
                ctx.fillStyle = clr;
                ctx.fillRect(x, y, pixel_width+overlap, pixel_height+overlap);
            }
        }
    });
}

function adjustResolution() {
    let maxDimension = Math.max(window.innerWidth, document.documentElement.scrollHeight);
    RESOLUTION = Math.max(128, maxDimension / 10); // Example adjustment, tweak as necessary
}

function resizeCanvas() {
    let cnvs = document.getElementById('bgcnvs');
    let hcnvs = document.getElementById('hdrcnvs');
    cnvs.width = window.innerWidth;
    cnvs.height = document.documentElement.scrollHeight;
    
    //adjustResolution();
    drawPerlinNoise(cnvs);
}

// Event listener for window resize
window.addEventListener('resize', resizeCanvas);

// Call resizeCanvas on page load to ensure the canvas is the correct size
resizeCanvas();



