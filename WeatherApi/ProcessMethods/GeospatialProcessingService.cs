using System.Diagnostics;
using System.IO;
using WeatherApi.Helper;

namespace WeatherApi.ProcessMethods;

public class GeospatialProcessingService{
    private readonly PostgreSqlService _dbService;
    private readonly string _tableName;

    public GeospatialProcessingService(PostgreSqlService service){
        _dbService = service;
        _tableName = "weather_raster";
    }

    //Calculates number of files to download, uses UrlService to get url equivalent to the number and
    //Starts to download and process each of them asynchronously
    public async Task DownloadAndProcessBatch(){
        try{
            await _dbService.EnsureTableExists(_tableName);
            DateTime forecastStartingTime = await GetStartingForecastTime();

            List<string> urls = UrlService.GenerateForecastUrls(forecastStartingTime, await GetNumOfFilesToDownload(forecastStartingTime));
            List<string> filePaths = new List<string>();
            foreach (var url in urls){
                filePaths.Add("./Data");
            }
            //After processing, insert to postGIS in bulk
            Stopwatch sw = new Stopwatch();
            sw.Start();
            await Parallel.ForEachAsync(urls.Zip(filePaths), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (pair, cancellationToken) => {
                    await DownloadAndProcess(pair.First, pair.Second);
                });
            sw.Stop();
            Console.WriteLine("Time taken: " + sw.ElapsedMilliseconds + "ms");
        }
        catch (Exception ex){
            Console.WriteLine($"Batch processing failed: {ex.Message}");
        }
    }

    //Downloads and processes each url grib2 file one by one
    async Task DownloadAndProcess(string url, string filePath){
        string tmpFileDir = "Temperature";
        string precipFileDir = "Precipitation";
        string windFileDir = "WindGust";
        
        string timestampName = ExtractDateTimeFromUrl(url).ToString("yyyyMMdd_HHmmss");
        
        string gribFilePath = Path.Combine(filePath, $"data_{timestampName}.grib2");
        string tifFilePath = Path.Combine(filePath, $"geoTiff_{timestampName}.tif");
        string epsgFilePath = Path.Combine(filePath, $"epsgGeoTiff_{timestampName}.tif");

        string colormapDir = "D:\\Programming\\Projects\\WeatherApi\\WeatherApi\\Colormaps";
        string temperatureColorMap = Path.Combine(colormapDir, "tmp-colormap.txt");
        string windspeedColorMap = Path.Combine(colormapDir, "wind-colormap.txt");
        string rainColorMap = Path.Combine(colormapDir, "rain-colormap.txt");
            
        string tmpFilePath = Path.Combine(filePath, $"tmpBand_{timestampName}.tif");
        string colorizedTmpFilePath = Path.Combine(filePath + $"/{tmpFileDir}", $"tmp_colorized_{timestampName}.tif");

        string windspeedFilePath = Path.Combine(filePath, $"windSpeedBand_{timestampName}.tif");
        string colorizedWindspeedFilePath = Path.Combine(filePath + $"/{windFileDir}", $"windSpeed_colorized_{timestampName}.tif");
        
        string rainFilePath = Path.Combine(filePath, $"precip_{timestampName}.tif");
        string colorizedRainFilePath = Path.Combine(filePath + $"/{precipFileDir}", $"precip_colorized_{timestampName}.tif");
        
        try{
            Directory.CreateDirectory(filePath);
            Directory.CreateDirectory(Path.Combine(filePath, tmpFileDir));
            Directory.CreateDirectory(Path.Combine(filePath, windFileDir));
            Directory.CreateDirectory(Path.Combine(filePath, precipFileDir));
            
            await GdalProcesses.DownloadAutomatic(url, gribFilePath);
            await GdalProcesses.ConvertToGeoTiff(gribFilePath, tifFilePath);
            await GdalProcesses.ConvertTifToProperSpatialRef(tifFilePath, epsgFilePath);

            //Extract and colorize wind map
            await GdalProcesses.ExtractBand(epsgFilePath, windspeedFilePath, 1);
            await GdalProcesses.Colorize(windspeedFilePath, colorizedWindspeedFilePath, windspeedColorMap);
            await GdalProcesses.UpdateIndex(colorizedWindspeedFilePath, "windgust_mosaic");
            //Extract and colorize temperature map
            await GdalProcesses.ExtractBand(epsgFilePath, tmpFilePath, 2);
            await GdalProcesses.Colorize(tmpFilePath, colorizedTmpFilePath, temperatureColorMap);
            await GdalProcesses.UpdateIndex(colorizedTmpFilePath, "temperature_mosaic");
            //Extract and colorize precipitation
            await GdalProcesses.ExtractBand(epsgFilePath, rainFilePath, 3);
            await GdalProcesses.Colorize(rainFilePath, colorizedRainFilePath, rainColorMap);
            await GdalProcesses.UpdateIndex(colorizedRainFilePath, "precipitation_mosaic");
            
            DateTime time = ExtractDateTimeFromUrl(url);
            // await _dbService.AddGeoTiffToPostGis(epsgFilePath, time, _tableName);

            CleanupProcessedFiles(new List<string>{tifFilePath, epsgFilePath, tmpFilePath, windspeedFilePath, rainFilePath});
        }
        catch (Exception e){
            Console.WriteLine("Download and process Error: " + e.Message);
        }

        void CleanupProcessedFiles(List<string> files){
            foreach (var file in files){
                if(File.Exists(file)) File.Delete(file);
            }
        }
    }

    //Gets the starting forecast datetime to start downloading from
    public async Task<DateTime> GetStartingForecastTime(){
        DateTime? latestForecastData = (await GetLatestForecastTimestamp(_tableName))?.ToUniversalTime();
        DateTime currTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
        DateTime startTime = latestForecastData ?? currTime;
        //Always use the latest time data
        if (DateTime.Compare(currTime, startTime) == 1){
            startTime = currTime;
        }
        return startTime;
        
        async Task<DateTime?> GetLatestForecastTimestamp(string tableName){
            if (!(await _dbService.TableExists(tableName))) return null;
            try{
                string query = $"SELECT MAX(time) FROM public.{tableName}";
                string? date = _dbService.GetData(query);
                if (string.IsNullOrEmpty(date)){
                    Console.WriteLine("Date is null");
                    return null;
                }
                return DateTime.Parse(date);
            }
            catch (Exception ex){
                Console.WriteLine("No database but still trying to get time");
                return null;
            }
        }
    }
    
    // If date in database is outdated, returns number of future forecast data to download
    // Always tries to have forecast data x hours from current time
    async Task<int> GetNumOfFilesToDownload(DateTime? latestForecastDate){
        int totalForecastHours = UrlService.NumOfFiles;

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
        await Task.Yield();
        return 0;
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