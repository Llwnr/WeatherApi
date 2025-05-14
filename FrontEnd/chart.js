import { map, currDateTime, activeOverlay, Overlays } from './main.js';

var recentLatlng = {
    lat: 0,
    lng: 0
}
const ChartType = {
    hourly: 'hourly',
    average: 'average',
}
var recentChartType = ChartType.hourly;
function getUrl(lat, lon, dateTime, chartType){
    lon = ((lon+180)%360+360)%360-180;
    const baseUrl = `https://localhost:7169/WeatherForecast/weather_details`;
    let coverageType = "";
    if(chartType == ChartType.hourly){
        if(activeOverlay == Overlays.Precipitation) coverageType = "rainHourly";
    }else if(chartType == ChartType.average){
        if(activeOverlay == Overlays.Temperature) coverageType = "avg";
        else coverageType = "rainAvg";
    }

    recentChartType = chartType;

    return `${baseUrl}?lat=${lat}&lon=${lon}&dateTimeStr=${dateTime}&dataCoverage=${coverageType}`;
}
window.addEventListener('load',(e)=>{
    recentLatlng = {
        lat: 0,
        lng: 0
    }
    var plotDiv = document.getElementById("chart");
    var chartOpenerButton = document.getElementById('open-graph');
    chartOpenerButton.addEventListener('click', function(){
        showChart()
        var dailyChartMode = document.getElementById('daily-graph');
        dailyChartMode.focus();
    })

    var averageChartMode = document.getElementById('avg-graph');
    averageChartMode.addEventListener('click', function(){
        const url = getUrl(recentLatlng.lat, recentLatlng.lng, currDateTime, ChartType.average);
        fetch(url)
            .then(response => {
                if(!response.ok) throw new Error("Http error: " + response.error);
                return response.json();
            })
            .then(data => {
                console.log(data)
                populateChartData(data, ChartType.average);
            })
    })
    var dailyChartMode = document.getElementById('daily-graph');
    dailyChartMode.addEventListener('click', function(){
        const url = getUrl(recentLatlng.lat, recentLatlng.lng, currDateTime, ChartType.hourly);
        fetch(url)
            .then(response => {
                if(!response.ok) throw new Error("Http error: " + response.error);
                return response.json();
            })
            .then(data => {
                console.log(data)
                populateChartData(data, ChartType.hourly);
            })
    })
})
if(map){
    map.on('move',()=>{
        hideChart();
    })
    map.on('click', function(e){
        hideChart();
        recentLatlng = e.latlng;
        let lat = e.latlng.lat;
        let lon = e.latlng.lng;
        lon = ((lon+180)%360+360)%360-180; //Clamping longitude as it can exceed normal bounds. Ex: 361 = 1

        const url = getUrl(recentLatlng.lat, recentLatlng.lng, currDateTime, ChartType.hourly);
        fetch(url)
            .then(response => {
                if(!response.ok) throw new Error("Http error: " + response.error);
                return response.json();
            })
            .then(data => {
                console.log(data)
                populateChartData(data, ChartType.hourly);
            })
    })

    map.on('baselayerchange',(e)=>{
        hideChart();
    })
}

function populateChartData(data, type){
    let values = data.value;

    let label = "Temperature";
    if(activeOverlay == Overlays.Precipitation) label = "Rain";

    let title = getTitle();
    let xLabel = type == ChartType.hourly ? 'Hour' : 'Day';
    let denotion = getDenotion();
    
    try{
        var weatherData = {
            x: data.time.map(time=>
                type == ChartType.hourly ? 
                    parseInt(time)+1 > 12 ? parseInt(time)-11+"PM" : parseInt(time)+1+"AM" //Adding +1hr cuz its +45min, which I then rounded to +60min/+1hr
                : time.toString()
            ),
            y: values,
            type: 'scatter',
            line:{
                color: activeOverlay == Overlays.Temperature ? 'rgb(243, 79, 24)' : 'rgb(0, 120, 255)', 
                smoothing: 0.4,
                shape:'spline',
            },
            marker:{color: values.map(value => 
                activeOverlay == Overlays.Precipitation 
                    ? 'rgb(0, 120, 255)'
                    : value < 0 ? 'rgb(108, 90, 240)' : 'rgb(243, 79, 24)'
            )},
            hovertemplate: `%{y:.2f} ${denotion}<extra></extra>`
        };
        
        let min = Math.min(...values);
        let max = Math.max(...values);

        if(min == max){min = max - 1; max = max+1;}

        let diff = max - min;
        min = min - Math.abs(diff)*0.4;
        max = max + Math.abs(diff)*0.4;
        
        const layout = {
            dragmode: 'pan',
            margin: {
                l: 60,
                r: 30,
                t: 60,
                b: 50
            },        
            font: { family: 'Poppins', size: 16, color: 'black'},
            title:{
                text: title,
                x: 0.5,
                xanchor: 'center',
                y: 0.95, 
                yanchor: 'top',
                pad: {
                    t: 20, // top padding
                    b: 10  // bottom padding
                },
            },
            yaxis:{
                range: [min, max],
                title:{
                    text: label + "  " + denotion,
                    color: 'green'
                },
                gridcolor: 'rgba(255, 255, 255, 0.7)',
                fixedrange: true
            },
            xaxis:{
                title:{
                    text: xLabel
                },
                type: 'category',
                gridcolor: 'rgba(255, 255, 255, 0.3)',
                range: type==ChartType.hourly? [7,19] : [3,8],
                fixedrange: false,
                tickfont:{
                    size: 14
                },
                dtick: type==ChartType.hourly? 2 : 1
            },
            paper_bgcolor: 'rgba(255, 255, 255, 0)',  // transparent background
            plot_bgcolor: 'rgba(255, 255, 255, 0.1)',
            hovermode: 'x unified',
            hoverlabel: {
                bgcolor: 'rgba(0, 0, 0, 0.7)', 
                font: { color: '#ffffff' }
            }
        };
        var myData = [weatherData];
        
        //Make the chart visible as well as clickable
        var plotDiv = document.getElementById("chart");
        if(plotDiv && plotDiv.__fullData && plotDiv.__fullData.length > 0){
            Plotly.react(plotDiv, myData, layout);
        }else{
            Plotly.newPlot(plotDiv, myData, layout);
        }
    }catch(err){
        console.log(err);
    }

    function getDenotion(){
        switch(activeOverlay){
            case Overlays.Temperature:
                return "°C";
            case Overlays.Wind:
                return " km/h";
            case Overlays.Precipitation:
                return " mm/h";
            default:
                break;
        }
    }

    function getTitle(){
        const currDate = new Date(currDateTime);
        switch(type){
            case ChartType.hourly:
                return `Hourly ${label}: `+currDate.getMonth().toString().padStart(2, "0")+"/"+currDate.getDate().toString().padStart(2,"0");
            case ChartType.average:
                return `Daily average ${label} of past 7 days`
        }
    }
}

function showChart(){
    var plotDiv = document.getElementById("chart");
    plotDiv.style.pointerEvents = 'auto';
    plotDiv.style.display= 'block';
}
function hideChart(){
    var plotDiv = document.getElementById("chart");
    plotDiv.style.pointerEvents = 'auto';
    plotDiv.style.display= 'none';
}
document.addEventListener('keydown', function(event) {
  if (event.ctrlKey && event.key === 'z') {
    console.log("ok");
    Plotly.relayout(plotDiv, {
      'xaxis.autorange': true,
      'yaxis.autorange': true
    });
  }
});

document.addEventListener('timeUpdated', function(event) {
    if(recentLatlng.lat === 0 && recentLatlng.lon === 0) return;
    const url = getUrl(recentLatlng.lat, recentLatlng.lng, currDateTime, recentChartType);
    fetch(url)
        .then(response => {
            if(!response.ok) throw new Error("Http error: " + response.error);
            return response.json();
        })
        .then(data => {
            populateChartData(data, recentChartType);
        })
        .catch(err => console.log("Msg: " + err.message));
});