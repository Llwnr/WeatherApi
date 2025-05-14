import { map, currDateTime, activeOverlay, Overlays } from './main.js';

if(map){
    map.on('click', function(e){
        hideDisplayBox();
        var lat = e.latlng.lat;
        var lon = e.latlng.lng;
        lon = ((lon+180)%360+360)%360-180;
        let weatherType = 'temperature';
        if(activeOverlay == Overlays.Wind) weatherType = 'windspeed';
        else if(activeOverlay == Overlays.Precipitation) weatherType = 'precipitation';

        const baseUrl = `https://localhost:7169/WeatherForecast/${weatherType}`;
        const url = `${baseUrl}?lat=${lat}&lon=${lon}&dateTimeStr=${currDateTime}`;
        fetch(url)
            .then(response => {
                if(!response.ok) throw new Error("Http error: " + response.error);
                return response.json();
            })
            .then(data => {
                const clickedPos = e.containerPoint;
                showDisplayBox(clickedPos, data.toFixed(1))
            })
    });
}
window.addEventListener('load',(event) => {
    const displayBox = document.getElementById('displayer');
    displayBox.addEventListener('mouseleave', () => {
        hideDisplayBox();
    })
});
function hideDisplayBox(){
    let displayBox = document.getElementById('displayer');
    displayBox.style.display = 'none';
}
function showDisplayBox(clickedPos, data){
    let displayBox = document.getElementById('displayer');
    let text = document.getElementById('displayer-text');

    let chartButton = document.getElementById('open-graph');
    if(activeOverlay == Overlays.Wind){//Don't show chart for wind
        chartButton.style.display = 'none';
    }else{
        chartButton.style.display = 'block';
    }

    displayBox.style.display = 'block';
    displayBox.style.left = `${clickedPos.x}px`;
    displayBox.style.top = `${clickedPos.y}px`;
    text.innerHTML = data;
    let denotion = "";
    switch(activeOverlay){
        case Overlays.Temperature:
            denotion = "°C";
            break;
        case Overlays.Wind:
            denotion = " km/h";
            break;
        case Overlays.Precipitation:
            denotion = " mm/h";
            break;
        default:
            break;
    }
    text.innerHTML += denotion;
}
