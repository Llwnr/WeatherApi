using System.Diagnostics;

namespace WeatherApi.ProcessMethods {
	public static class GdalProcesses {
		public static async Task DownloadAutomatic(string url, string outputFilePath) {
			
			using (HttpClient client = new HttpClient()) {
				try{
					client.Timeout = TimeSpan.FromSeconds(20);
					HttpResponseMessage res = await client.GetAsync(url);
					res.EnsureSuccessStatusCode();

					using (Stream stream = await res.Content.ReadAsStreamAsync())
					using (FileStream fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write)) {
						await stream.CopyToAsync(fileStream);
						Console.WriteLine("File downloaded successfully.");
					}
				}
				catch (Exception ex) {
					Console.WriteLine($"Download Error: {ex.Message}\nUrl: {url}");
				}
			}
		}

		public static async Task ConvertToGeoTiff(string inputFilePath, string outputFilePath) {
			string argument = $"gdal_translate -b 1 -b 3 -b 4 -of GTiff -r bilinear -co NUM_THREADS=ALL_CPUS {inputFilePath} {outputFilePath}";
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
		
		public static async Task ExtractBand(string inputFilePath, string outputFilePath, int band){
			string extractionArgument = $"gdal_translate -b {band} -tr 0.1 0.1 -r cubicspline -co NUM_THREADS=ALL_CPUS {inputFilePath} {outputFilePath}";
			try{
				await ProcessExecution.ExecuteCommand(extractionArgument);
			}
			catch (Exception ex){
				Console.WriteLine("Colorizing error: " + ex.Message);
			}
		}
		
		public static async Task Colorize(string inputFilePath, string outputFilePath, string colormapFilePath){
			string argument = $"gdaldem color-relief -co NUM_THREADS=ALL_CPUS -co COMPRESS=DEFLATE -co TILED=YES {inputFilePath} {colormapFilePath} {outputFilePath}";
			try{
				await ProcessExecution.ExecuteCommand(argument);
			}
			catch (Exception ex){
				Console.WriteLine("Colorizing error: " + ex.Message);
			}
		}

		public static async Task UpdateIndex(string inputFilePath, string storeName){
			string uri = $"http://localhost:8080/geoserver/rest/workspaces/ne/coveragestores/{storeName}/external.imagemosaic";
			string headers = "@{'Content-Type'='text/plain'}";
			string credential = "(New-Object System.Management.Automation.PSCredential('admin', (ConvertTo-SecureString 'geoserver' -AsPlainText -Force)))";
			string powerShellCommand = 
				"Invoke-RestMethod " +
				$"-Uri '{uri}' " +
				"-Method Post " +
				$"-Headers {headers} " +
				$"-Body '{Path.GetFullPath(inputFilePath)}' " +
				$"-Credential {credential}";
			
			ProcessStartInfo processInfo = new ProcessStartInfo{
				FileName = "powershell.exe",
				Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{powerShellCommand}\"",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (Process process = new Process()){
				try{
					process.StartInfo = processInfo;
					process.Start();

					// string output = await process.StandardOutput.ReadToEndAsync();
					// string error = await process.StandardError.ReadToEndAsync();
					
					// Console.WriteLine("Output: " + output);
					// Console.WriteLine("Error: " + error);
					
					await process.WaitForExitAsync();
				}
				catch (Exception ex){
					Console.WriteLine("Error in updating index: " + ex.Message);
				}
			}
		}

		// public static async Task<DateTime> GetDateTime(string inputFilePath) {
		// 	DateTime fileTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
		//
		// 	string argument = $"gdalinfo \"{inputFilePath}\" | grep GRIB_VALID_TIME";
		// 	ProcessStartInfo timeOfFile = ProcessExecution.CreateNewProcess(argument);
		// 	using (Process getTimeOfFile = Process.Start(timeOfFile)) {
		// 		while (!getTimeOfFile.StandardOutput.EndOfStream) {
		// 			string line = getTimeOfFile.StandardOutput.ReadLine();
		// 			string[] parts = line.Split('=');
		// 			if (parts.Length > 1) {
		// 				long unixTime = long.Parse(parts[1].Trim());
		// 				fileTime = fileTime.AddSeconds(unixTime).ToLocalTime();
		// 				Console.WriteLine(fileTime);
		// 				break;
		// 			}
		// 		}
		// 		await getTimeOfFile.WaitForExitAsync();
		// 	}
		// 	
		// 	return fileTime;
		// }
	}
}
