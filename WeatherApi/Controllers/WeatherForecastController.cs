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
    public IActionResult GetTemp([FromQuery] double lat, [FromQuery] double lon, [FromQuery] string dateTimeStr){
        DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        dateTime = dateTime.ToLocalTime();
        string query = $@"SELECT ST_VALUE(rast, 2, ST_MakePoint({lon}, {lat})) 
            FROM public.weather_raster
            WHERE extract(hour from time) = {dateTime.Hour} AND extract(day from time) = {dateTime.Day};
            ";
        string? result = _dbService.GetData(query);
        if (result == null){
            return BadRequest();
        }
        return Ok(_dbService.GetData(query));
    }
    [HttpGet("windspeed")]
    public IActionResult GetWindSpeed([FromQuery] double lat, [FromQuery] double lon, [FromQuery] string dateTimeStr){
        DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        dateTime = dateTime.ToLocalTime();
        string query = $@"SELECT ST_VALUE(rast, 1, ST_MakePoint({lon}, {lat})) 
            FROM public.weather_raster
            WHERE extract(hour from time) = {dateTime.Hour} AND extract(day from time) = {dateTime.Day};
            ";
        string? result = _dbService.GetData(query);
        if (result == null){
            return BadRequest();
        }
        return Ok(_dbService.GetData(query));
    }
    [HttpGet("precipitation")]
    public IActionResult GetPrecipitation([FromQuery] double lat, [FromQuery] double lon, [FromQuery] string dateTimeStr){
        DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        dateTime = dateTime.ToLocalTime();
        string query = $@"SELECT ST_VALUE(rast, 3, ST_MakePoint({lon}, {lat})) 
            FROM public.weather_raster
            WHERE extract(hour from time) = {dateTime.Hour} AND extract(day from time) = {dateTime.Day};
            ";
        string? result = _dbService.GetData(query);
        if (result == null){
            return BadRequest();
        }
        return Ok(_dbService.GetData(query));
    }
    
    [HttpGet("wind_animation")]
    public async Task<IActionResult> GetWindAnimationFile([FromQuery] string dateTimeStr){ // Make the method async
        string dir = "./Data/UV_Wind";
        string prefix = "uvWind_";

        try{
            DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
            string timeString = dateTime.ToString("yyyyMMdd_HHmmss");

            string fullPath = Path.Combine(dir, prefix + timeString + ".json");

            if (System.IO.File.Exists(fullPath)){
                var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                return File(fileStream, "application/json");
            }
            else{
                return NotFound(new { Error = "No wind animation file found for the requested time" });
            }
        }
        catch (Exception ex){
            Console.WriteLine("Error: " + ex.Message);
            return StatusCode(500, new { Error = "An internal server error occurred.", Details = ex.Message });
        }
    }
}
