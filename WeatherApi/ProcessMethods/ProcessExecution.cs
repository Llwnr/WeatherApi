using System.Diagnostics;
namespace WeatherApi.ProcessMethods {
	public static class ProcessExecution {
		public static async Task ExecuteCommand(string command) {
			ProcessStartInfo processInfo = CreateNewProcess(command);

			await RunProcess(processInfo);
		}

		public static async Task ExecuteCommand(string command, string psqlPassword) {
			ProcessStartInfo processInfo = CreateNewProcess(command);
			processInfo.EnvironmentVariables["PGPASSWORD"] = psqlPassword;
			await RunProcess(processInfo);
		}

		async static Task RunProcess(ProcessStartInfo processInfo) {
			using (Process process = Process.Start(processInfo)) {
				string error = await process.StandardError.ReadToEndAsync();

				await process.WaitForExitAsync();

				if (!string.IsNullOrEmpty(error)){
					Console.WriteLine("Errors: " + "\nArgument: " + processInfo.Arguments);
				}

				if (process.ExitCode != 0) {
					throw new Exception($"Command failed with exit code {process.ExitCode}:\n{error}");
				}
			}
		}

		public static ProcessStartInfo CreateNewProcess(string command) {
			ProcessStartInfo processInfo = new ProcessStartInfo {
				FileName = "cmd.exe",
				Arguments = $"/C {command}",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			return processInfo;
		}
		
	}
}
