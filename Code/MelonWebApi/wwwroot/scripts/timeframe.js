// Timeframe selection //
let selectedTimePeriod = 'day'; // Default to 'day'

function handlePeriodSelection(period) {
    selectedTimePeriod = period;
    reloadPageData(); // Function to reload data based on the selected period
}

// Updated event listeners for period buttons
document.getElementById('day').addEventListener('click', () => handlePeriodSelection('day'));
document.getElementById('week').addEventListener('click', () => handlePeriodSelection('week'));
document.getElementById('month').addEventListener('click', () => handlePeriodSelection('month'));
document.getElementById('year').addEventListener('click', () => handlePeriodSelection('year'));

// Function to reload data across the site based on the selected time period
function reloadPageData() {
    // Example: Update the listening time per period display
    displayListeningTimePerPeriod();

    // Call other update functions as needed, e.g., to update top stats, recent activity, etc.
    populateTopStats();
    populateRecentActivity();
    // Note: You'll need to adjust these functions to use the selectedTimePeriod variable
    // to fetch and display the correct data.
}