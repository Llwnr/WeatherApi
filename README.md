Weather Data visualization project.

## Dependencies to install:
GDAL - I recommend installing GDAL from OSGeo4w for windows. Make sure it runs through CLI. \
GeoServer v2.27 or above.\
PostGIS\
Python\

Data source is from NOAA's NOMADS GFS model.\
Resolution is 0.25\

This project uses C# to automate weather data processing and store it in GeoServer locally, leaflet.js to render tiles from GeoServer.

## Setup Instructions(Before running the C# program): <br/>
You need to have a PostGIS database and set it up in C# backend, in the appsettings.json file's connection string.<br/><br/>
You need to link GeoServer's stores to the folders where colorized temperature, wind and precipitation are stored. <br>
 - Go to WeatherApi/Config/Backup and Copy all the folders.
 - Next, go to WeatherApi and create a new folder called "Data".
 - Paste the recently copied folders inside the Data folder.
 - Open GeoServer and create 3 stores. Each for temperature, wind and precipitation.
 - Link the stores to each folder. This connects GeoServer to use your processed colorized image.
In the same FrontEnd/main.js folder, make sure to put maptiler api in case you want city borders/ boundaries.\
That's all! \

Snippets:
![image](https://github.com/user-attachments/assets/5eba21cf-1c7a-4963-82b2-382e09958f3a)
![image](https://github.com/user-attachments/assets/15ee5572-d10f-478f-94a0-8e0dc6f0919b)


