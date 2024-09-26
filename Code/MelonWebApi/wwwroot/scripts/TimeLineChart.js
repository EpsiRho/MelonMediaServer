const cox = document.getElementById('myChart').getContext('2d');

// Hour labels for the x-axis
const labels = [
    '1am', '2am', '3am', '4am', '5am', '6am',
    '7am', '8am', '9am', '10am', '11am', '12pm',
    '1pm', '2pm', '3pm', '4pm', '5pm', '6pm',
    '7pm', '8pm', '9pm', '10pm', '11pm', '12am'
];

// Sample data for minutes listened per hour
const minutesListened = [5, 10, 3, 0, 0, 15, 30, 45, 60, 55, 40, 20, 25, 35, 50, 65, 70, 55, 40, 30, 20, 15, 10, 5];

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
            max: 80,
            ticks: {
                stepSize: 10
            }
        }
    }
};

// Create the Chart.js line chart
const myChart = new Chart(cox, {
    type: 'line',
    data: data,
    options: options
});