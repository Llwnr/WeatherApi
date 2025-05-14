var southWest = L.latLng(-100,-10200);
var northEast = L.latLng(79,7200);
var bounds = L.latLngBounds(southWest, northEast);

var map = L.map('map',{
        center: [10, 75],
        zoom: 3,
        minZoom: 3,
        maxZoom: 12,
        maxBounds: bounds,
        maxBoundsViscosity: 0.98
    });
// console.log(map.getBounds())
// Add base map
// https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer/tile/{z}/{y}/{x}
// https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}

map.createPane('labels');
const labelsPane = map.getPane('labels');
labelsPane.style.zIndex = 0;
labelsPane.style.pointerEvents = 'none';
labelsPane.style.filter = 'invert(1) brightness(2.5)';

map.createPane('borders');
const bordersPane = map.getPane('borders');
bordersPane.style.zIndex = 600;

var worldMap = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}', {
    maxZoom: 16,
    attribution: 'Tiles © Esri — Source: Esri',
    opacity: 1
});
var arcGisHillshade = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Shaded_Relief/MapServer/tile/{z}/{y}/{x}', {
    maxZoom: 16,
    attribution: 'Tiles © Esri — Source: Esri',
    opacity: 0.3
});
arcGisHillshade.addTo(map);
var cartoPositronLabels = L.tileLayer('https://{s}.basemaps.cartocdn.com/light_only_labels/{z}/{x}/{y}{r}.png', {
    attribution: '© <a href="https://carto.com/attributions">CARTO</a>',
    subdomains: 'abcd',
    maxZoom: 16,
    pane: 'labels'
});
cartoPositronLabels.addTo(map);

//Adding the vector/boundary layer
var mapTilerApiKey = '67lUbeSuJxuVWYZyU6X2';
var mapTilerStyleUrl = `https://api.maptiler.com/maps/01967181-6f82-78ea-aae7-f23f9891f04c/style.json?key=${mapTilerApiKey}`;
var mapTilerVectorLayer = L.maplibreGL({
    style: mapTilerStyleUrl,
    attribution: '<a href="https://www.maptiler.com/copyright/" target="_blank">© MapTiler</a> <a href="https://www.openstreetmap.org/copyright" target="_blank">© OpenStreetMap contributors</a>', // Add required attribution
    pane: 'borders'
}).addTo(map);

// Overlays
const Overlays = {
    None: "none",
    Temperature: "temperature",
    Wind: "wind",
    Precipitation: "precip"
};
var activeOverlay = Overlays.Temperature;

var geoserverApi = 'http://localhost:8080/geoserver/wms';
const overlayOptions = {
    format: 'image/png',
    transparent: true,
    className: 'wms-multiply-overlay', 
    maxZoom: 16
};
var tmpOverlay = L.tileLayer.wms(geoserverApi, {
    layers: 'ne:temperature_mosaic',
    ...overlayOptions
});
tmpOverlay.addTo(map)
var windOverlay = L.tileLayer.wms(geoserverApi, {
    layers: 'ne:windgust_mosaic',
    ...overlayOptions
});
var precipOverlay = L.tileLayer.wms(geoserverApi, {
    layers: 'ne:precipitation_mosaic',
    ...overlayOptions
});

// Define layers for the layer control
var weatherOverlays = {
    "Temperature Overlay": tmpOverlay,
    "Wind Gust Overlay": windOverlay,
    "Rain Overlay": precipOverlay,
};
//Add the layer control to the map
var layersControl = L.control.layers(weatherOverlays, {}).addTo(map);

map.on('baselayerchange', function(e){
    let overlayName = e.name;
    try{
        if(overlayName == "Rain Overlay"){
            activeOverlay = Overlays.Precipitation;
            precipOverlay.setParams({time: currDateTime});
            if (velocityLayerAdded) {
                map.removeLayer(velocityLayer);
                velocityLayerAdded = false;
            }

            ChangeRainAnimation();

            //Switch base map for rain
            if(map.hasLayer(arcGisHillshade)){
                map.removeLayer(arcGisHillshade);
                worldMap.addTo(map);
                setTimeout(() => worldMap.bringToBack(),100);
            }
            
        }else if(overlayName == "Wind Gust Overlay"){
            activeOverlay = Overlays.Wind;
            windOverlay.setParams({time: currDateTime});
            
            if (velocityLayerAdded) {
                map.removeLayer(velocityLayer);
                velocityLayerAdded = false;
            }
            ChangeWindAnimation();
            map.removeLayer(tmpOverlay);

            if(map.hasLayer(worldMap)){
                map.removeLayer(worldMap);
                map.addLayer(arcGisHillshade);
                arcGisHillshade.bringToBack();
            }
        }else{
            activeOverlay = Overlays.Temperature;
            tmpOverlay.setParams({time: currDateTime});
            map.removeLayer(velocityLayer);
            velocityLayerAdded = false;

            if(map.hasLayer(worldMap)){
                map.removeLayer(worldMap);
                map.addLayer(arcGisHillshade);
                arcGisHillshade.bringToBack();
            }
        }
    }catch(err){
        console.log("Error. Overlay probably doesn't exist ", err);
    }
});

let currDateTime;
let layerUpdateTimeout = null;
let loadingLayer = null;//Keep track of the new layer thats loading
//Update Parameters when changing date/time
document.addEventListener('timeUpdated', function(event) {
    clearTimeout(layerUpdateTimeout);
    if(loadingLayer && map.hasLayer(loadingLayer)){
        map.removeLayer(loadingLayer);
    }
    loadingLayer = null;

    const timeString = event.detail.time;
    currDateTime = timeString;
    if(activeOverlay == Overlays.Wind) ChangeWindAnimation();

    let layerToUpdate, layerType, wmsLayerName, currentActiveOverlayEnum;
    switch(activeOverlay) {
        case Overlays.Temperature:
            layerToUpdate = tmpOverlay;
            layerType = "Temperature Overlay";
            wmsLayerName = 'ne:temperature_mosaic';
            currentActiveOverlayEnum = Overlays.Temperature;
            break;
        case Overlays.Wind:
            layerToUpdate = windOverlay;
            layerType = "Wind Gust Overlay";
            wmsLayerName = 'ne:windgust_mosaic';
            currentActiveOverlayEnum = Overlays.Wind;
            break;
        case Overlays.Precipitation:
            layerToUpdate = precipOverlay;
            layerType = "Rain Overlay";
            wmsLayerName = 'ne:precipitation_mosaic';
            currentActiveOverlayEnum = Overlays.Precipitation;
            break;
        default:
            return; // No active overlay known
    }

    // Call the function to handle the smooth update
    smoothlyUpdateWMSLayer(layerToUpdate, layerType, wmsLayerName, currentActiveOverlayEnum);
});

function ChangeWindAnimation(){
    const baseUrl = 'https://localhost:7169/WeatherForecast/wind_animation';
    const url = `${baseUrl}?dateTimeStr=${currDateTime}`;
    fetch(url)
        .then(response => {
            if(!response.ok) throw new Error("Http error: " + response.error);
            return response.json();
        })
        .then(data => {
            setParticleAnimationDirect(data, windParams);
        })
        .catch(error => {
            console.log("Error fetching wind data: " + error);
        })
}
const smoothlyUpdateWMSLayer = (oldLayerInstance, layerType, wmsLayerName, overlayEnum) => {
    const newLayer = L.tileLayer.wms(geoserverApi, {
        ...overlayOptions,
        layers: wmsLayerName,
        time: currDateTime
    });

    // Mark this layer as the one currently loading
    loadingLayer = newLayer;

    const targetOpacity = (overlayOptions && typeof overlayOptions.opacity !== 'undefined') ? overlayOptions.opacity : 1;

    newLayer.setOpacity(0);
    newLayer.addTo(map);

    newLayer.once('load', () => {
        // Check if this layer is still the one we intend to load
        if (loadingLayer !== newLayer) {
            // A newer update has started, this layer is obsolete. Remove if still on map.
            if (map.hasLayer(newLayer)) {
                map.removeLayer(newLayer);
            }
            return; // Stop processing
        }

        // Clear any potentially lingering timeout (safety net)
        clearTimeout(layerUpdateTimeout);

        layerUpdateTimeout = setTimeout(() => {
            // Final check: Ensure this layer is still the designated loadingLayer
            if (loadingLayer !== newLayer) {
                 if (map.hasLayer(newLayer)) {
                    map.removeLayer(newLayer);
                 }
                 return; // Abort if a newer update took over
            }

            // Perform the swap
            newLayer.setOpacity(targetOpacity);

            // Remove the old layer from map and control
            if (oldLayerInstance && map.hasLayer(oldLayerInstance)) {
                map.removeLayer(oldLayerInstance);
                layersControl.removeLayer(oldLayerInstance);
            }

            // Add the new layer to the control
            layersControl.addBaseLayer(newLayer, layerType); // Use addBaseLayer as per original code

            // Update the correct global variable to reference the new layer
            switch(overlayEnum) {
                case Overlays.Temperature:
                    tmpOverlay = newLayer;
                    break;
                case Overlays.Wind:
                    windOverlay = newLayer;
                    break;
                case Overlays.Precipitation:
                    precipOverlay = newLayer;
                    break;
            }

            // Reset tracking variables
            loadingLayer = null;
            layerUpdateTimeout = null;

        }, 150);
    });
};


function ChangeRainAnimation(){
    setParticleAnimation("./rain.json", rainParams);
}

//Animations
let velocityLayer;//Global variable for animation layer
const windParams = {
    velocityScale: 0.007,
    particleMultiplier: 0.003, //How many particles to display. More is more
    particleAge: 25,
    particlelineWidth: 1.8,
    opacity: 1,
    displayValues: false
}
const rainParams = {
    velocityScale: 0.05,
    particleMultiplier: 0.001, //How many particles to display. More is more
    particleAge: 10,
    particlelineWidth: 1,
    opacity: 0.3,
    displayValues: false
}
let velocityLayerAdded = false;
function updateVelocityLayer(animData, params){
    velocityLayer.setOptions({
        data: animData,
        // displayValues: params.displayValues,
        displayValues: false,
        velocityScale: params.velocityScale,
        particleMultiplier: params.particleMultiplier,
        particleAge: params.particleAge,
        particleLineWidth: params.particlelineWidth,
        opacity: params.opacity,
        frameRate: 30, // Default is often 15
        colorScale: [
            "rgb(255, 255, 255)",    
            "rgb(255, 255, 255)",  
        ]
    });
}
function setParticleAnimationDirect(myData, options = windParams){
    try{
        if (!L.velocityLayer) {
                console.error("L.velocityLayer is not available.");
                return;
            }
        if(velocityLayerAdded){
            updateVelocityLayer(myData, options);
            return;
        }

        velocityLayer = L.velocityLayer({
            displayValues: options.displayValues,
            displayOptions: {
                velocityType: 'Wind',
                position: 'bottomleft',
                emptyString: 'No wind data',
                angleConvention: 'bearingCW',
                speedUnit: 'km/hr'
            },
            data: myData,

            // --- Parameters ---
            velocityScale: options.velocityScale,
            particleMultiplier: options.particleMultiplier, //How many particles to display. More is more
            particleAge: options.particleAge,
            particlelineWidth: options.particlelineWidth,
            opacity: options.opacity,
            frameRate: 30, // Default is often 15
            colorScale: [
                "rgb(255, 255, 255)",    
                "rgb(255, 255, 255)",  
            ]
        });
        if(!velocityLayerAdded){
            velocityLayer.addTo(map);
            velocityLayerAdded = true;
            console.log("Creating velocity layer")
        }
    }catch(error){
        console.error("Error processing wind data: ", error);
    }
}
//Parameters for rain and wind animation
function setParticleAnimation(dataFileLocation, options = windParams){
    fetch(dataFileLocation)
    .then(response => {
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status} ${response.statusText}.`);
        }
        // console.log("wind_flipped.json fetched successfully. Parsing JSON");
        return response.json();
    })
    .then(data => {
        setParticleAnimationDirect(data, options);
    })
    .catch(error => {
        console.error("Error processing wind data:", error);
    });
}
export { map, currDateTime, activeOverlay, Overlays };