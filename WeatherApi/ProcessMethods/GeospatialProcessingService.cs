using System.Diagnostics;
using System.IO;
using WeatherApi.Helper;

namespace WeatherApi.ProcessMethods;

public class GeospatialProcessingService{
    private readonly PostgreSqlService _dbService;

    public GeospatialProcessingService(PostgreSqlService service){
        _dbService = service;
    }

    async Task<DateTime?> GetLatestForecastTimestamp(string tableName){
        if (!(await _dbService.TableExists(tableName))) return null;
        string query = $"SELECT MAX(time) FROM public.{tableName}";
        string? date =_dbService.GetData(query);
        if (string.IsNullOrEmpty(date)) return null;
        return DateTime.Parse(date);
    }
    
    // If date in database is outdated, returns number of future forecast data to download
    // Always tries to have forecast data 12 hours from current time
    async Task<int> GetNumOfFilesToDownload(){
        int totalForecastHours = UrlService.NumOfFiles;

        DateTime? latestForecastDate = await GetLatestForecastTimestamp("weather_raster_test");
        //If no data found, download full number of files
        if (latestForecastDate == null){
            Console.WriteLine($"No existing forecast data. Downloading {totalForecastHours + 1} files.");
            return totalForecastHours+1;
        }
        
        long latestDatabaseTimestamp = ((DateTimeOffset)latestForecastDate).ToUnixTimeSeconds();
        long currentTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
        
        int hoursDifference = (int)Math.Ceiling((latestDatabaseTimestamp - currentTimestamp) / 3600.0);
        
        if (hoursDifference+1 < totalForecastHours){
            int filesToDownload = Math.Min(totalForecastHours - hoursDifference, totalForecastHours);
            Console.WriteLine($"Forecast data is incomplete. Downloading {filesToDownload} additional files.");
            return filesToDownload;
        }
        
        Console.WriteLine("No need to download");
        return 0;
    }

    public async Task DownloadAndProcessBatch(){
        try{
            DateTime? latestForecastData = (await GetLatestForecastTimestamp("weather_raster_test"))?.ToUniversalTime();
            DateTime currTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
            DateTime startTime = latestForecastData ?? currTime;
            //Always use the latest time data
            if (DateTime.Compare(currTime, startTime) == 1){
                startTime = currTime;
            }

            List<string> urls = UrlService.GenerateForecastUrls(startTime, await GetNumOfFilesToDownload());
            List<string> filePaths = new List<string>();
            foreach (var url in urls){
                string timestampFolder = ExtractDateTimeFromUrl(url).ToString("yyyyMMdd_HHmmss");
                filePaths.Add(Path.Combine("./Data", timestampFolder));
            }
            //After processing, insert to postGIS in bulk
            Stopwatch sw = new Stopwatch();
            sw.Start();
            await _dbService.EnsureTableExists("weather_raster_test");
            await Parallel.ForEachAsync(urls.Zip(filePaths), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (pair, cancellationToken) => {
                    await DownloadAndProcess(pair.First, pair.Second);
                });
            sw.Stop();
            Console.WriteLine("Time taken: " + sw.ElapsedMilliseconds);
        }
        catch (Exception ex){
            Console.WriteLine($"Batch processing failed: {ex.Message}");
        }
    }

    async Task DownloadAndProcess(string url, string filePath){
        try{
            Console.WriteLine("Creating directory at: " + filePath);
            Directory.CreateDirectory(filePath);

            string gribFilePath = Path.Combine(filePath, "data.grib2");
            string tifFilePath = Path.Combine(filePath, "geoTiff.tif");
            string epsgFilePath = Path.Combine(filePath, "epsgGeoTiff.tif");

            await GdalProcesses.DownloadAutomatic(url, gribFilePath);
            await GdalProcesses.ConvertToGeoTiff(gribFilePath, tifFilePath);
            await GdalProcesses.ConvertTifToProperSpatialRef(tifFilePath, epsgFilePath);
            
            DateTime time = ExtractDateTimeFromUrl(url);
            await _dbService.AddGeoTiffToPostGis(epsgFilePath, time, "weather_raster_test");

            CleanupProcessedFiles(new List<string>{tifFilePath,epsgFilePath});
        }
        catch (Exception e){
            Console.WriteLine("Processing Error: " + e.Message);
        }

        void CleanupProcessedFiles(List<string> files){
            foreach (var file in files){
                if(File.Exists(file)) File.Delete(file);
            }
        }
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