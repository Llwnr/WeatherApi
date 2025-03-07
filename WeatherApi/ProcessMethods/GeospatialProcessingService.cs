using WeatherApi.Helper;

namespace WeatherApi.ProcessMethods;

public class GeospatialProcessingService{
    private readonly PostgreSqlService _dbService;

    public GeospatialProcessingService(PostgreSqlService service){
        _dbService = service;
    }

    DateTime? GetLatestForecastTimestamp(){
        string query = "SELECT MAX(time) FROM public.weather_raster";
        string? date =_dbService.GetData(query);
        if (string.IsNullOrEmpty(date)) return null;
        return DateTime.Parse(date);
    }
    
    // If date in database is outdated, returns number of future forecast data to download
    // Always tries to have forecast data 12 hours from current time
    int GetNumOfFilesToDownload(){
        int totalForecastHours = UrlService.NumOfFiles;

        DateTime? latestForecastDate = GetLatestForecastTimestamp();
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
        DateTime? latestForecastData = GetLatestForecastTimestamp()?.ToUniversalTime();
        DateTime currTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
        DateTime startTime = latestForecastData ?? currTime;
        //Always use the latest time data
        if (DateTime.Compare(currTime, startTime) == 1){
            startTime = currTime;
        }
        
        List<string> urls = UrlService.GenerateForecastUrls(startTime, GetNumOfFilesToDownload());
        foreach (var url in urls){
            await DownloadAndProcess(url);
        }
    }

    async Task DownloadAndProcess(string url){
        string filePath = "./Data";
        string fileName = "data.grib2";
        await GdalProcesses.DownloadAutomatic(url, filePath, fileName);
        await GdalProcesses.ConvertToGeoTiff(filePath, fileName, "out.tif");
        await GdalProcesses.ConvertTifToProperSpatialRef(filePath, "out.tif", "outepsg.tif");
        DateTime time = await GdalProcesses.GetDateTime(filePath, fileName);

        await _dbService.AddGeoTiffToPostGIS($"./Data\\outepsg.tif", time, "weather_raster_test");
    }
}