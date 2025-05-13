using Microsoft.AspNetCore.Mvc;
using WeatherApi.Helper;
using WeatherApi.Model;

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
        try{
            DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
            dateTime = dateTime.ToLocalTime();
            string query = $@"SELECT ST_VALUE(rast, 2, ST_MakePoint({lon}, {lat})) 
                FROM public.weather_raster
                WHERE extract(hour from time) = {dateTime.Hour} AND extract(day from time) = {dateTime.Day};
                ";
            string? result = _dbService.GetData(query);
            if (result == null) return BadRequest();
            return Ok(result);
        }
        catch (Exception ex){
            Console.WriteLine("Error in getting temperature: " + ex.Message);
            return BadRequest();
        }
    }
    [HttpGet("windspeed")]
    public IActionResult GetWindSpeed([FromQuery] double lat, [FromQuery] double lon, [FromQuery] string dateTimeStr){
        try{
            DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
            dateTime = dateTime.ToLocalTime();
            string query = $@"SELECT ST_VALUE(rast, 1, ST_MakePoint({lon}, {lat}))*3.6 
                FROM public.weather_raster
                WHERE extract(hour from time) = {dateTime.Hour} AND extract(day from time) = {dateTime.Day};
                ";
            string? result = _dbService.GetData(query);
            if (result == null) return BadRequest();
            return Ok(result);
        }
        catch (Exception ex){
            Console.WriteLine("Error in getting temperature: " + ex.Message);
            return BadRequest();
        }
    }
    [HttpGet("precipitation")]
    public IActionResult GetPrecipitation([FromQuery] double lat, [FromQuery] double lon, [FromQuery] string dateTimeStr){
        try{
            DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
            dateTime = dateTime.ToLocalTime();
            string query = $@"SELECT ST_VALUE(rast, 3, ST_MakePoint({lon}, {lat})) 
                FROM public.weather_raster
                WHERE extract(hour from time) = {dateTime.Hour} AND extract(day from time) = {dateTime.Day};
                ";
            string? result = _dbService.GetData(query);
            if (result == null) return BadRequest();
            
            result = (double.Parse(result) * 3750).ToString();
            return Ok(result);
        }
        catch (Exception ex){
            Console.WriteLine("Error in getting temperature: " + ex.Message);
            return BadRequest();
        }
    }
    [HttpGet("weather_details")]
    public IActionResult GetDetailedData([FromQuery] double lat, [FromQuery] double lon, [FromQuery] string dateTimeStr, [FromQuery] string dataCoverage = null){
        try{
            DateTime dateTime = DateTime.Parse(dateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
            dateTime = dateTime.ToLocalTime();
            string query = "";
            if(String.IsNullOrEmpty(dataCoverage)) query = QueryConstants.GetCurrentHourlyTemperatureData(lat, lon, dateTime);
            else if (dataCoverage == "avg"){
                query = QueryConstants.GetDailyAverageTemperatureDataOfAWeek(lat, lon, dateTime);
            }else if (dataCoverage == "rainHourly"){
                query = QueryConstants.GetTotalRainAmount(lat, lon, dateTime);
            }else if (dataCoverage == "rainAvg"){
                query = QueryConstants.GetDailyAverageRain(lat, lon, dateTime);
            }

            List<WeatherData> weatherDatas = _dbService.GetData<WeatherData>(query);
            var result = new{
                time = weatherDatas.Select(data => data.Time).ToList(),
                value = weatherDatas.Select(data => data.Value).ToList()
            };
            return Ok(result);
        }
        catch (Exception ex){
            Console.WriteLine("Error in getting temperature: " + ex.Message);
            return BadRequest();
        }
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

    [HttpGet("time_range")]
    public async Task<IActionResult> GetTimeRange(){
        //return the min and max time for datas that u have
        try{
            string queryMax = $@"SELECT MAX(time) FROM weather_raster";
            string? resultMax = _dbService.GetData(queryMax);
            if (resultMax == null) return BadRequest();
            
            string queryMin = $@"SELECT MIN(time) FROM weather_raster";
            string? resultMin = _dbService.GetData(queryMin);
            if (resultMin == null) return BadRequest();
            
            return Ok(new {
                Max = resultMax,
                Min = resultMin
            });
        }
        catch (Exception ex){
            Console.WriteLine("Error in getting temperature: " + ex.Message);
            return BadRequest();
        }
    }
}
