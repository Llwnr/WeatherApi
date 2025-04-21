using Microsoft.AspNetCore.Mvc;
using WeatherApi.Helper;

namespace WeatherApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase{
    private readonly PostgreSqlService _dbService;

    public WeatherForecastController(PostgreSqlService service){
        _dbService = service;
    }

    [HttpGet("temperature")]
    public string GetTemp([FromQuery] double lat, [FromQuery] double lon, [FromQuery] int hourOfDay){
        string query = $@"SELECT ST_VALUE(rast, 2, ST_SetSRID(ST_MakePoint({lon}, {lat}), 4326)) 
            FROM public.weather_raster
            WHERE extract(hour from time) = {hourOfDay} AND extract(day from time) = 11;
            ";
        return _dbService.GetData(query);
    }
    [HttpGet("windspeed")]
    public string GetWindSpeed([FromQuery] double lat, [FromQuery] double lon, [FromQuery] int hourOfDay){
        string query = $@"SELECT ST_VALUE(rast, 1, ST_SetSRID(ST_MakePoint({lon}, {lat}), 4326)) 
            FROM public.weather_raster
            WHERE extract(hour from time) = {hourOfDay} AND extract(day from time) = 11;
            ";
        return _dbService.GetData(query);
    }
    [HttpGet("precipitation")]
    public string GetPrecipitation([FromQuery] double lat, [FromQuery] double lon, [FromQuery] int hourOfDay){
        string query = $@"SELECT ST_VALUE(rast, 3, ST_SetSRID(ST_MakePoint({lon}, {lat}), 4326)) 
            FROM public.weather_raster
            WHERE extract(hour from time) = {hourOfDay} AND extract(day from time) = 11;
            ";
        return _dbService.GetData(query);
    }
}
