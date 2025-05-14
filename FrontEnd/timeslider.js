const dateDisplay = document.getElementById('date-display');
const hourDisplay = document.getElementById('hour-display');
const minuteDisplay = document.getElementById('minute-display');
const picker = document.querySelector('.datetime-picker');

//Initialize min and max constraints
var minDate = new Date(Date.UTC(2023, 0, 1, 0));
var maxDate = new Date(Date.UTC(2025, 11, 31, 23)); 

window.onload = function(){
    updateDisplay();

    const apiUrl = `https://localhost:7169/WeatherForecast/time_range`;
    fetch(apiUrl)
        .then(response => {
            if(!response.ok) throw new Error("Http error: " + response.error);
            return response.json();
        })
        .then(data => {
            const offsetMinutes = 5 * 60 + 45;
            const offsetMs = offsetMinutes * 60 * 1000;

            minDate = new Date(new Date(data.min).getTime() - offsetMs);
            maxDate = new Date(new Date(data.max).getTime() - offsetMs);
        });

}

let date = new Date();
let currentDate = new Date(date.getFullYear(), date.getMonth(), date.getDate(), date.getUTCHours()); //Year month day hour minute

const monthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

function getUTCStringForWMS(date) {
    // Use UTC methods to get components 
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0'); 
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');

    // Force minutes and seconds to 00
    return `${year}-${month}-${day}T${hours}:00:00Z`;
}

function updateDisplay() {
    let localeDate = new Date(currentDate.getTime() - currentDate.getTimezoneOffset()*60000);
    const day = localeDate.getDate();
    const month = monthNames[localeDate.getMonth()];
    const hours = String(localeDate.getHours()).padStart(2, '0');
    const minutes = String(localeDate.getMinutes()).padStart(2, '0');

    dateDisplay.textContent = `${day} ${month}`;
    hourDisplay.textContent = hours;
    minuteDisplay.textContent = minutes;

    const timeUpdateEvent = new CustomEvent('timeUpdated',{
        detail: {time: getUTCStringForWMS(currentDate), dateTime: currentDate}
    });
    document.dispatchEvent(timeUpdateEvent);
}

picker.addEventListener('click', (event) => {
    if (!event.target.matches('.arrow-button')) return;

    const button = event.target;
    const unit = button.dataset.unit;
    const direction = button.classList.contains('up') ? 1 : -1;

    let newDate = new Date(currentDate);

    switch (unit) {
        case 'date':
            newDate.setDate(newDate.getDate() + direction);
            break;
        case 'hour':
            newDate.setHours(newDate.getHours() + direction);
            break;
    }
    if (newDate >= minDate && newDate <= maxDate) {
        currentDate = newDate;
        updateDisplay();
    }
});