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
        string query = $@"SELECT ST_VALUE(rast, ST_SetSRID(ST_MakePoint({lon}, {lat}), 4326)) 
FROM public.weather_raster
WHERE extract(hour from time) = {hourOfDay};
";
        return _dbService.GetTemperature(query);
    }
}
