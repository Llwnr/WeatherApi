using System.Security.Cryptography.X509Certificates;
using WeatherApi.Helper;

namespace WeatherApi.ProcessMethods;

public class GeospatialProcessingService{
    private readonly PostgreSqlService _dbService;

    public GeospatialProcessingService(PostgreSqlService service){
        _dbService = service;
    }

    DateTime? GetLatestForecastTimestamp(string tableName){
        string query = $"SELECT MAX(time) FROM {tableName}";
        string? date =_dbService.GetData(query);
        if (string.IsNullOrEmpty(date)) return null;
        return DateTime.Parse(date);
    }
    
    // If date in database is outdated, returns number of future forecast data to download
    // Always tries to have forecast data 12 hours from current time
    int GetNumOfFilesToDownload(){
        int totalForecastHours = UrlService.NumOfFiles;

        DateTime? latestForecastDate = GetLatestForecastTimestamp("public.weather_raster_test");
        //If no data found, download full number of files
        if (latestForecastDate == null){
            Console.WriteLine($"No existing forecast data. Downloading {totalForecastHours + 1} files.");
            return totalForecastHours+1;
        }
        
        long latestDatabaseTimestamp = ((DateTimeOffset)latestForecastDate).ToUnixTimeSeconds();
        long currentTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
        
        int hoursDifference = (int)Math.Ceiling((latestDatabaseTimestamp - currentTimestamp) / 3600.0);
        
        if (hoursDifference < totalForecastHours){
            int filesToDownload = Math.Min(totalForecastHours - hoursDifference, totalForecastHours);
            Console.WriteLine($"Forecast data is incomplete. Downloading {filesToDownload} additional files.");
            return filesToDownload;
        }
        
        Console.WriteLine("No need to download");
        return 0;
    }

    public async Task DownloadAndProcessBatch(){
        try{
            DateTime? latestForecastData = GetLatestForecastTimestamp("public.weather_raster_test")?.ToUniversalTime();
            DateTime currTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
            DateTime startTime = latestForecastData ?? currTime;
            //Always use the latest time data
            if (DateTime.Compare(currTime, startTime) == 1){
                startTime = currTime;
            }

            List<string> urls = UrlService.GenerateForecastUrls(startTime, GetNumOfFilesToDownload());

            for (int i = 0; i < urls.Count; i++){
                await DownloadAndProcess(urls[i]);
            }
        }
        catch (Exception ex){
            Console.WriteLine($"Batch download and processing failed: {ex.Message}");
        }
    }

    async Task DownloadAndProcess(string url){
        string timestampFolder = ExtractDateTimeFromUrl(url).ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine("./Data", timestampFolder);
        Directory.CreateDirectory(filePath);
        
        string gribFilePath = Path.Combine(filePath, "data.grib2");
        string tifFilePath = Path.Combine(filePath, "geoTiff.tif");
        string epsgFilePath = Path.Combine(filePath, "epsgGeoTiff.tif");
        
        await GdalProcesses.DownloadAutomatic(url, gribFilePath);
        await GdalProcesses.ConvertToGeoTiff(gribFilePath, tifFilePath);
        await GdalProcesses.ConvertTifToProperSpatialRef(tifFilePath, epsgFilePath);
        DateTime time = await GdalProcesses.GetDateTime(gribFilePath);

        await _dbService.AddGeoTiffToPostGIS(epsgFilePath, time, "weather_raster_test");
    }
    
    public static DateTime ExtractDateTimeFromUrl(string url){
        string datePattern = @"gfs\.(\d{8})";
        string cyclePattern = @"t(\d{2})z";
        string forecastPattern = @"f(\d{3})";

        var dateMatch = System.Text.RegularExpressions.Regex.Match(url, datePattern);
        var cycleMatch = System.Text.RegularExpressions.Regex.Match(url, cyclePattern);
        var forecastMatch = System.Text.RegularExpressions.Regex.Match(url, forecastPattern);

        if (!dateMatch.Success || !cycleMatch.Success || !forecastMatch.Success)
            throw new ArgumentException("Invalid URL format");

        string dateStr = dateMatch.Groups[1].Value;
        int cycleHour = int.Parse(cycleMatch.Groups[1].Value);
        int forecastHour = int.Parse(forecastMatch.Groups[1].Value);

        DateTime baseDate = DateTime.ParseExact(dateStr, "yyyyMMdd", null).AddHours(cycleHour);
        return baseDate.AddHours(forecastHour);
    }

}