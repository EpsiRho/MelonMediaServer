// Helper function to clamp values within a range
function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

// Function to calculate the distance between two colors in RGB space
function getColorDistance(c1, c2) {
    return Math.sqrt(
        Math.pow(c1[0] - c2[0], 2) +
        Math.pow(c1[1] - c2[1], 2) +
        Math.pow(c1[2] - c2[2], 2)
    );
}

// Function to convert RGB to HSL color space
function rgbToHsl(r, g, b) {
    // Convert RGB values from 0-255 to 0-1
    r /= 255;
    g /= 255;
    b /= 255;

    const max = Math.max(r, g, b),
        min = Math.min(r, g, b);
    let h = 0,
        s = 0,
        l = (max + min) / 2;

    if (max !== min) {
        const d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

        switch (max) {
            case r:
                h = ((g - b) / d + (g < b ? 6 : 0));
                break;
            case g:
                h = (b - r) / d + 2;
                break;
            case b:
                h = (r - g) / d + 4;
                break;
        }

        h *= 60;
    }

    return { h, s, l };
}

// Function to convert HSL back to RGB color space
function hslToRgb(h, s, l) {
    let r, g, b;

    h = (h % 360 + 360) % 360; // Ensure h is within 0-360
    h /= 360;

    if (s === 0) {
        r = g = b = l * 255; // Achromatic
    } else {
        const hue2rgb = (p, q, t) => {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6) return p + (q - p) * 6 * t;
            if (t < 1 / 2) return q;
            if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
            return p;
        };

        const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        const p = 2 * l - q;

        r = hue2rgb(p, q, h + 1 / 3) * 255;
        g = hue2rgb(p, q, h) * 255;
        b = hue2rgb(p, q, h - 1 / 3) * 255;
    }

    return { r, g, b };
}

// Function to create a new color similar to an initial color but distinct
function createSimilarDistinctColor(initialColor) {
    const { h, s, l } = rgbToHsl(initialColor[0], initialColor[1], initialColor[2]);

    const hueShift = Math.random() * 30 - 15; // Between -15 and +15 degrees
    const newHue = (h + hueShift + 360) % 360;

    const saturationShift = Math.random() * 0.2 - 0.1; // Between -0.1 and +0.1
    const lightnessShift = Math.random() * 0.2 - 0.1; // Between -0.1 and +0.1
    const newSaturation = clamp(s + saturationShift, 0, 1);
    const newLightness = clamp(l + lightnessShift, 0, 1);

    const { r, g, b } = hslToRgb(newHue, newSaturation, newLightness);

    return [ Math.round(r), Math.round(g), Math.round(b) ];
}

// Main function to extract colors from an image
function getColors(image, numberOfColors) {
    const width = image.width;
    const height = image.height;

    // Create a canvas to draw the image and extract pixel data
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');

    // Draw the image onto the canvas
    ctx.drawImage(image, 0, 0, width, height);

    // Get pixel data from the canvas
    const imageData = ctx.getImageData(0, 0, width, height);
    const pixels = imageData.data; // Uint8ClampedArray

    const colorCount = {};

    // Process each pixel
    for (let i = 0; i < pixels.length; i += 4) {
        // Quantize colors by reducing the number of possible values
        const r = pixels[i] & 0xE0;     // Red component
        const g = pixels[i + 1] & 0xE0; // Green component
        const b = pixels[i + 2] & 0xE0; // Blue component

        // Filter out gray colors
        if (Math.abs(r - g) < 20 && Math.abs(g - b) < 20 && Math.abs(r - b) < 20) {
            continue;
        }

        // Create a unique key for the color
        const colorKey = `${r},${g},${b}`;

        // Count the occurrence of each color
        if (colorCount[colorKey]) {
            colorCount[colorKey]++;
        } else {
            colorCount[colorKey] = 1;
        }
    }

    // Sort colors by frequency in descending order
    const sortedColors = Object.entries(colorCount)
        .map(([colorKey, count]) => ({ colorKey, count }))
        .sort((a, b) => b.count - a.count);

    const distinctColors = [];

    // Select the most frequent and distinct colors
    for (const { colorKey } of sortedColors) {
        if (distinctColors.length >= numberOfColors) {
            break;
        }

        const [r, g, b] = colorKey.split(',').map(Number);

        let isDistinct = true;
        for (const selectedColor of distinctColors) {
            const distance = getColorDistance([ r, g, b ], selectedColor);
            console.log(distance);
            if (distance < 100) {
                isDistinct = false;
                break;
            }
        }

        if (isDistinct) {
            distinctColors.push([ r, g, b ]);
        }
    }

    // If not enough colors are found, create similar distinct colors
    if (distinctColors.length < numberOfColors && distinctColors.length > 0) {
        const baseColor = distinctColors[0];
        while (distinctColors.length < numberOfColors) {
            distinctColors.push(createSimilarDistinctColor(baseColor));
        }
    }

    if (distinctColors.length < numberOfColors && distinctColors.length === 0) {
        const baseColor = [0, 0, 0];
        while (distinctColors.length < numberOfColors) {
            distinctColors.push(createSimilarDistinctColor(baseColor));
        }
    }

    return distinctColors;
}
