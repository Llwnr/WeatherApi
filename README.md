Weather Data visualization project.

## Dependencies:
GDAL - I recommend installing GDAL from OSGeo4w for windows. Make sure it runs through CLI. 
GeoServer v2.27 or above.
PostGIS
Python 

Data source is from NOAA's NOMADS GFS model. 
Resolution is 0.25

This project uses C# to automate weather data processing and store it in GeoServer locally, leaflet.js to render tiles from GeoServer.

Setup Instructions:
You need to have a PostGIS database and set it up in C# backend, in the PostgreSqlService.cs file.
You need to link GeoServer's stores to the folders where colorized temperature, wind and precipitation are stored. Make sure to have the store names be the same as the one used in FrontEnd/main.js where GeoServer's api is called.
In the same FrontEnd/main.js folder, make sure to put maptiler api in case you want city borders/ boundaries.
That's all!

Snippets:
![image](https://github.com/user-attachments/assets/5eba21cf-1c7a-4963-82b2-382e09958f3a)
![image](https://github.com/user-attachments/assets/15ee5572-d10f-478f-94a0-8e0dc6f0919b)


