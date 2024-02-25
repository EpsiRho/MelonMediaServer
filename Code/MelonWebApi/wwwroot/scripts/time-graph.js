// Listening time graph //
// Function to prepare data for the graph
function prepareGraphData(listeningTimeStats) {
    const labels = listeningTimeStats.map((item, index) => Object.keys(item)[0]);
    const data = listeningTimeStats.map(item => Object.values(item)[0]);

    return { labels, data };
}

// Function to render the graph
function renderListeningTimeGraph() {
    const { labels, data } = prepareGraphData(listeningTimeStats);
    const ctx = document.getElementById('timeOfDayGraph').getContext('2d');
    const timeOfDayGraph = new Chart(ctx, {
        type: 'bar', // or 'line', 'doughnut', etc., depending on your preference
        data: {
            labels: labels,
            datasets: [{
                label: 'Listening Time (minutes)',
                data: data,
                backgroundColor: 'rgba(54, 162, 235, 0.2)',
                borderColor: 'rgba(54, 162, 235, 1)',
                borderWidth: 1
            }]
        },
        options: {
            scales: {
                y: {
                    beginAtZero: true
                }
            }
        }
    });
}

// Call the function to render the graph
document.addEventListener('DOMContentLoaded', renderListeningTimeGraph);