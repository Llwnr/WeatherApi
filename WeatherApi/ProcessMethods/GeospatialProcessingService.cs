using System.Diagnostics;
using System.Globalization;
using System.IO;
using WeatherApi.Helper;

namespace WeatherApi.ProcessMethods;

public class GeospatialProcessingService : BackgroundService{
    private readonly PostgreSqlService _dbService;
    private readonly string _tableName;

    public GeospatialProcessingService(PostgreSqlService service){
        _dbService = service;
        _tableName = "weather_raster";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken){
        try{
            await DownloadAndProcessBatch();
            // await ProcessExistingFilesBatch();
        }
        catch (Exception ex){
            Console.WriteLine("Error processing batch: " + ex.Message);
        }
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
        string uVFileDir = "UV_Wind";
        
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
        string rainFilePathScaled = Path.Combine(filePath, $"precipScaled_{timestampName}.tif");
        string colorizedRainFilePath = Path.Combine(filePath + $"/{precipFileDir}", $"precip_colorized_{timestampName}.tif");

        string uvWindFilePath = Path.Combine(filePath + $"/{uVFileDir}", $"uvWind_{timestampName}.grib2");
        string uvWindRegriddedPath = Path.Combine(filePath + $"/{uVFileDir}", $"uvWindRegrid_{timestampName}.grib2");
        string uvJSONFilePath = Path.Combine(filePath + $"/{uVFileDir}", $"uvWind_{timestampName}.json");
        string postGisFilePath = Path.Combine(filePath, $"3banddata_{timestampName}.tif");
        try{
            Directory.CreateDirectory(filePath);
            Directory.CreateDirectory(Path.Combine(filePath, tmpFileDir));
            Directory.CreateDirectory(Path.Combine(filePath, windFileDir));
            Directory.CreateDirectory(Path.Combine(filePath, precipFileDir));
            Directory.CreateDirectory(Path.Combine(filePath, uVFileDir));
            
            await GdalProcesses.DownloadAutomatic(url, gribFilePath);
            await GdalProcesses.ConvertToGeoTiff(gribFilePath, tifFilePath);
            await GdalProcesses.ConvertTifToProperSpatialRef(tifFilePath, epsgFilePath);

            //Extract and colorize wind map
            await GdalProcesses.ExtractBand(epsgFilePath, windspeedFilePath, 1);
            await GdalProcesses.Colorize(windspeedFilePath, colorizedWindspeedFilePath, windspeedColorMap);
            await GdalProcesses.UpdateIndex(colorizedWindspeedFilePath, "windgust_mosaic");
            //Extract and colorize temperature map
            await GdalProcesses.ExtractBand(epsgFilePath, tmpFilePath, 3);
            await GdalProcesses.Colorize(tmpFilePath, colorizedTmpFilePath, temperatureColorMap);
            await GdalProcesses.UpdateIndex(colorizedTmpFilePath, "temperature_mosaic");
            //Extract and colorize precipitation
            await GdalProcesses.ExtractBand(epsgFilePath, rainFilePath, 6);
            await GdalProcesses.Scale(rainFilePath, rainFilePathScaled, "-scale 0 0.016 0 60");
            await GdalProcesses.Colorize(rainFilePathScaled, colorizedRainFilePath, rainColorMap);
            await GdalProcesses.UpdateIndex(colorizedRainFilePath, "precipitation_mosaic");
            
            //Extract U V of wind for directions
            await GdalProcesses.ExtractBand(gribFilePath, uvWindFilePath, "-b 4 -b 5", true);
            //Regrid the uvWind file for smaller size
            await GdalProcesses.RegridResolution(uvWindFilePath, uvWindRegriddedPath);
            //Take the extracted file and convert into json
            await GdalProcesses.ConvertToJson(uvWindRegriddedPath, uvJSONFilePath);
            
            //Extract bands
            await GdalProcesses.ExtractBand(gribFilePath, postGisFilePath, "-b 1 -b 3 -b 6");
            
            DateTime time = ExtractDateTimeFromUrl(url);//Its in UTC
            await _dbService.AddGeoTiffToPostGis(postGisFilePath, time, _tableName);

            CleanupProcessedFiles(new List<string>{tifFilePath, postGisFilePath, epsgFilePath, tmpFilePath, windspeedFilePath, rainFilePath, rainFilePathScaled, uvWindFilePath, uvWindRegriddedPath});
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
        DateTime? latestForecastData = await GetLatestForecastTimestamp(_tableName);
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
                string query = $"SELECT (MAX(time) AT TIME ZONE 'UTC') FROM public.{tableName}";
                string? date = _dbService.GetData(query);
                if (string.IsNullOrEmpty(date)){
                    Console.WriteLine("Date is null");
                    return null;
                }

                DateTime dateUTC = DateTime.Parse(date);
                if (dateUTC.Minute != 0){//If local time, convert to UTC
                    dateUTC = dateUTC.ToUniversalTime();
                }
                dateUTC = DateTime.SpecifyKind(dateUTC, DateTimeKind.Utc);
                Console.WriteLine(dateUTC);
                return dateUTC;
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
    
    public static DateTime ExtractDateTimeFromFileName(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string pattern = @"data_(\d{8})_(\d{6})\.grib2";
        var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern);

        if (!match.Success || match.Groups.Count != 3)
            throw new ArgumentException($"Invalid filename format: {fileName}");

        string dateStr = match.Groups[1].Value;
        string timeStr = match.Groups[2].Value;
        string dateTimeStr = dateStr + timeStr;

        DateTime result = DateTime.ParseExact(dateTimeStr, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return DateTime.SpecifyKind(result, DateTimeKind.Utc);
    }

    public async Task ProcessExistingFilesBatch(){
        try{
            await _dbService.EnsureTableExists(_tableName);
            string dataDirectory = "./Data";

            if (!Directory.Exists(dataDirectory))
            {
                 Console.WriteLine($"Data directory not found: {dataDirectory}");
                 return;
            }

            var gribFiles = Directory.GetFiles(dataDirectory, "data_*.grib2");

            if (gribFiles == null || !gribFiles.Any()){
                Console.WriteLine($"No .grib2 files found in {dataDirectory}");
                return;
            }

            Console.WriteLine($"Found {gribFiles.Length} files to process.");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            await Parallel.ForEachAsync(gribFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (gribFilePath, cancellationToken) =>
                {
                    string filePath = dataDirectory;
                    string tmpFileDir = "Temperature";
                    string precipFileDir = "Precipitation";
                    string windFileDir = "WindGust";
                    string uVFileDir = "UV_Wind";

                    DateTime time;
                    string timestampName;
                    try{
                        time = ExtractDateTimeFromFileName(gribFilePath);
                        timestampName = time.ToString("yyyyMMdd_HHmmss");
                    }
                    catch (ArgumentException ex){
                        Console.WriteLine($"Skipping file due to invalid name format: {gribFilePath}. Error: {ex.Message}");
                        return;
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"Error extracting time from {gribFilePath}: {ex.Message}");
                         return;
                    }

                    string tifFilePath = Path.Combine(filePath, $"geoTiff_{timestampName}.tif");
                    string epsgFilePath = Path.Combine(filePath, $"epsgGeoTiff_{timestampName}.tif");

                    string colormapDir = "D:\\Programming\\Projects\\WeatherApi\\WeatherApi\\Colormaps";
                    string temperatureColorMap = Path.Combine(colormapDir, "tmp-colormap.txt");
                    string windspeedColorMap = Path.Combine(colormapDir, "wind-colormap.txt");
                    string rainColorMap = Path.Combine(colormapDir, "rain-colormap.txt");

                    string tmpFilePath = Path.Combine(filePath, $"tmpBand_{timestampName}.tif");
                    string colorizedTmpFilePath = Path.Combine(filePath, tmpFileDir, $"tmp_colorized_{timestampName}.tif");

                    string windspeedFilePath = Path.Combine(filePath, $"windSpeedBand_{timestampName}.tif");
                    string colorizedWindspeedFilePath = Path.Combine(filePath, windFileDir, $"windSpeed_colorized_{timestampName}.tif");

                    string rainFilePath = Path.Combine(filePath, $"precip_{timestampName}.tif");
                    string rainFilePathScaled = Path.Combine(filePath, $"precipScaled_{timestampName}.tif");
                    string colorizedRainFilePath = Path.Combine(filePath, precipFileDir, $"precip_colorized_{timestampName}.tif");

                    string uvWindFilePath = Path.Combine(filePath, uVFileDir, $"uvWind_{timestampName}.grib2");
                    string uvWindRegriddedPath = Path.Combine(filePath, uVFileDir, $"uvWindRegrid_{timestampName}.grib2");
                    string uvJSONFilePath = Path.Combine(filePath, uVFileDir, $"uvWind_{timestampName}.json");
                    string postGisFilePath = Path.Combine(filePath, $"3banddata_{timestampName}.tif");

                    try{
                        Directory.CreateDirectory(Path.Combine(filePath, tmpFileDir));
                        Directory.CreateDirectory(Path.Combine(filePath, windFileDir));
                        Directory.CreateDirectory(Path.Combine(filePath, precipFileDir));
                        Directory.CreateDirectory(Path.Combine(filePath, uVFileDir));

                        await GdalProcesses.ConvertToGeoTiff(gribFilePath, tifFilePath);
                        await GdalProcesses.ConvertTifToProperSpatialRef(tifFilePath, epsgFilePath);

                        await GdalProcesses.ExtractBand(epsgFilePath, windspeedFilePath, 1);
                        await GdalProcesses.Colorize(windspeedFilePath, colorizedWindspeedFilePath, windspeedColorMap);
                        await GdalProcesses.UpdateIndex(colorizedWindspeedFilePath, "windgust_mosaic");

                        await GdalProcesses.ExtractBand(epsgFilePath, tmpFilePath, 3);
                        await GdalProcesses.Colorize(tmpFilePath, colorizedTmpFilePath, temperatureColorMap);
                        await GdalProcesses.UpdateIndex(colorizedTmpFilePath, "temperature_mosaic");

                        await GdalProcesses.ExtractBand(epsgFilePath, rainFilePath, 6);
                        await GdalProcesses.Scale(rainFilePath, rainFilePathScaled, "-scale 0 0.016 0 60");
                        await GdalProcesses.Colorize(rainFilePathScaled, colorizedRainFilePath, rainColorMap);
                        await GdalProcesses.UpdateIndex(colorizedRainFilePath, "precipitation_mosaic");

                        await GdalProcesses.ExtractBand(gribFilePath, uvWindFilePath, "-b 4 -b 5", true);
                        await GdalProcesses.RegridResolution(uvWindFilePath, uvWindRegriddedPath);
                        await GdalProcesses.ConvertToJson(uvWindRegriddedPath, uvJSONFilePath);

                        await GdalProcesses.ExtractBand(gribFilePath, postGisFilePath, "-b 1 -b 3 -b 6");

                        await _dbService.AddGeoTiffToPostGis(postGisFilePath, time, _tableName);

                        CleanupProcessedFilesLocal(new List<string>{tifFilePath, postGisFilePath, epsgFilePath, tmpFilePath, windspeedFilePath, rainFilePath, rainFilePathScaled, uvWindFilePath, uvWindRegriddedPath});
                    }
                    catch (Exception e){
                        Console.WriteLine($"Error processing file {gribFilePath}: {e.Message}");
                    }
                });

            sw.Stop();
            Console.WriteLine("Batch processing of existing files finished. Time taken: " + sw.ElapsedMilliseconds + "ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Batch processing of existing files failed: {ex.Message}");
        }

        void CleanupProcessedFilesLocal(List<string> files){
            foreach (var file in files){
                 try
                 {
                      if(File.Exists(file)) File.Delete(file);
                 }
                 catch (IOException ioEx)
                 {
                     Console.WriteLine($"Error deleting file {file}: {ioEx.Message}");
                 }
                  catch (UnauthorizedAccessException uaEx)
                 {
                     Console.WriteLine($"Permission error deleting file {file}: {uaEx.Message}");
                 }
            }
        }
    }

}