import {map, activeOverlay, Overlays } from './main.js';

const tempColorIndex = 
    `<span>°C</span>
    <span>-30</span>
    <span>-20</span>
    <span>-10</span>
    <span>0</span>
    <span>10</span>
    <span>20</span>
    <span>30</span>
    <span>40</span>
    <span>50</span>`;

const windSpeedColorIndex = 
    `<span>Km/hr</span>
    <span>0</span>
    <span>30</span>
    <span>60</span>
    <span>90</span>
    <span>120</span>`;

const precipColorIndex = 
    `<span>mm/hr</span>
    <span>0</span>
    <span>2</span>
    <span>6</span>
    <span>12</span>
    <span>25</span>`;


window.addEventListener('load',(e)=>{
    var colorIndex = document.getElementById('color-index');

    map.on('baselayerchange', (e) => {
        switch(activeOverlay){
            case Overlays.Temperature:
                colorIndex.innerHTML = tempColorIndex;
                colorIndex.classList.remove("precipitation")
                colorIndex.classList.remove("wind-speed")
                colorIndex.classList.add("temperature")
                break;
            case Overlays.Wind:
                colorIndex.innerHTML = windSpeedColorIndex;
                colorIndex.classList.add("wind-speed")
                colorIndex.classList.remove("temperature")
                colorIndex.classList.remove("precipitation")
                break;
            case Overlays.Precipitation:
                colorIndex.innerHTML = precipColorIndex;
                colorIndex.classList.remove("temperature")
                colorIndex.classList.remove("wind-speed")
                colorIndex.classList.add("precipitation")
                break;
            default:
                colorIndex.innerHTML = "";
                break;
        }
    });
});