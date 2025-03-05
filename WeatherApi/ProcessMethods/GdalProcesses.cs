using System.Diagnostics;

namespace WeatherApi.ProcessMethods {
	public static class GdalProcesses {
		public static async Task DownloadAutomatic(string url, string filePath, string fileName) {
			Console.WriteLine("Link: " + url);
			Directory.CreateDirectory("./Data");

			string fileSavePath = $"{filePath}\\{fileName}";

			using (HttpClient client = new HttpClient()) {
				try {
					HttpResponseMessage res = await client.GetAsync(url);
					res.EnsureSuccessStatusCode();

					using (Stream stream = await res.Content.ReadAsStreamAsync())
					using (FileStream fileStream = new FileStream(fileSavePath, FileMode.Create, FileAccess.Write)) {
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

		public static async Task ConvertToGeoTiff(string filePath, string fileName, string outputFile) {
			string fullPath = Path.Combine(filePath, fileName);

			string argument = $"gdal_translate -of GTiff -r bilinear {fullPath} {filePath}\\{outputFile}";
			await ProcessExecution.ExecuteCommand(argument);
		}

		public static async Task ConvertTifToProperSpatialRef(string filePath, string fileName, string outputFile) {
			string fullPath = Path.Combine(filePath, fileName);
			
			string argument = $"gdalwarp -t_srs EPSG:4326 {fullPath} {filePath}\\{outputFile}";
			await ProcessExecution.ExecuteCommand(argument);
		}

		public static async Task<DateTime> GetDateTime(string filePath, string fileName) {
			string fullPath = Path.Combine(filePath, fileName);
			DateTime fileTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

			string argument = $"gdalinfo \"{fullPath}\" | grep GRIB_VALID_TIME";
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
