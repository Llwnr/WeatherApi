using System.Diagnostics;

namespace WeatherApi.ProcessMethods {
	public static class GdalProcesses {
		public static async Task DownloadAutomatic(string url, string outputFilePath) {

			using (HttpClient client = new HttpClient()) {
				try {
					HttpResponseMessage res = await client.GetAsync(url);
					res.EnsureSuccessStatusCode();

					using (Stream stream = await res.Content.ReadAsStreamAsync())
					using (FileStream fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write)) {
						await stream.CopyToAsync(fileStream);
						Console.WriteLine("File downloaded successfully.");
					}
				}
				catch (Exception ex) {
					Console.WriteLine($"Download Error: {ex.Message}");
				}
			}
			return;
		}

		public static async Task ConvertToGeoTiff(string inputFilePath, string outputFilePath) {
			string argument = $"gdal_translate -of GTiff -r bilinear -co NUM_THREADS=ALL_CPUS {inputFilePath} {outputFilePath}";
			try{
				await ProcessExecution.ExecuteCommand(argument);
			}
			catch (Exception ex){
				Console.WriteLine("Converting to geotiff error: " + ex.Message);
			}
		}

		public static async Task ConvertTifToProperSpatialRef(string inputFilePath, string outputFilePath) {
			string argument = $"gdalwarp -t_srs EPSG:4326 -co NUM_THREADS=ALL_CPUS {inputFilePath} {outputFilePath}";
			try{
				await ProcessExecution.ExecuteCommand(argument);
			}
			catch (Exception ex){
				Console.WriteLine("Converting to correct spatial reference error: " + ex.Message);
			}
		}

		public static async Task<DateTime> GetDateTime(string inputFilePath) {
			DateTime fileTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

			string argument = $"gdalinfo \"{inputFilePath}\" | grep GRIB_VALID_TIME";
			ProcessStartInfo timeOfFile = ProcessExecution.CreateNewProcess(argument);
			using (Process getTimeOfFile = Process.Start(timeOfFile)) {
				while (!getTimeOfFile.StandardOutput.EndOfStream) {
					string line = getTimeOfFile.StandardOutput.ReadLine();
					string[] parts = line.Split('=');
					if (parts.Length > 1) {
						long unixTime = long.Parse(parts[1].Trim());
						fileTime = fileTime.AddSeconds(unixTime).ToLocalTime();
						Console.WriteLine(fileTime);
						break;
					}
				}
				await getTimeOfFile.WaitForExitAsync();
			}
			
			return fileTime;
		}
	}
}
