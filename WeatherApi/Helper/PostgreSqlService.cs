using System.Diagnostics;
using Npgsql;
using WeatherApi.ProcessMethods;

namespace WeatherApi.Helper {
	public class PostgreSqlService {
		NpgsqlConnection conn;
		string connString = "";

		public PostgreSqlService(string connString) {
			this.connString = connString;
		}

		public async Task AddGeoTiffToPostGIS(string inputFilePath, DateTime time, string tableName) {
			string sqlFileOutput = "./Data\\output.sql";

			bool tableExists = false;
			using (conn = new NpgsqlConnection(connString)){
				await conn.OpenAsync();
				string query = @$"SELECT EXISTS(
					SELECT FROM information_schema.tables
					WHERE table_schema='public'
					AND table_name='{tableName}'
				)";
				using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn)){
					tableExists = (bool)await cmd.ExecuteScalarAsync();
				}
			}
			

			Console.WriteLine("Time: " + time);
			
			Process process = new Process();
			process.StartInfo.FileName = "cmd.exe";
			string append = tableExists ? " -a " : "";

// Set up the command with proper password handling for the pipe
			process.StartInfo.Arguments = $"/C set \"PGPASSWORD=livewithme0\" && " +
			                              $"raster2pgsql -s 4326 -I -C -M -F {append}{inputFilePath} public.{tableName} | " +
			                              $"psql -U postgres -h localhost -d weather -p 5432";

			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = false;

// Execute the command
			process.Start();
			await process.WaitForExitAsync();

			string addTimeColumnQuery = $@"
				ALTER TABLE IF EXISTS public.{tableName}
				ADD COLUMN IF NOT EXISTS time timestamp;	
			";
			await ExecuteSqlCommand(addTimeColumnQuery);

			string dateTime = time.ToString("yyyy-MM-dd HH:mm:ss");
			string updateTimeQuery = $@"UPDATE public.{tableName} 
				SET time = '{dateTime}' 
				WHERE rid = (SELECT rid FROM public.{tableName} ORDER BY rid DESC LIMIT 1);
				";
			await ExecuteSqlCommand(updateTimeQuery);
			Console.WriteLine("Inserted into database");
		}

		

		private async Task ExecuteSqlCommand(string query) {
			using (conn = new NpgsqlConnection(connString)) {
				await conn.OpenAsync();
				using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn)) {
					await cmd.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task ExecuteScalar(string query){
			using (conn = new NpgsqlConnection(connString)) {
				await conn.OpenAsync();
				using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn)) {
					await cmd.ExecuteScalarAsync();
				}
			}
		}

		public string? GetData(string query){
			try{
				using (conn = new NpgsqlConnection(connString)){
					conn.Open();
					using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn)){
						NpgsqlDataReader reader = cmd.ExecuteReader();
						string result = "";
						if (reader.HasRows){
							while (reader.Read()){
								result += reader[0]?.ToString();
							}
						}

						return result;
					}
				}
			}
			catch (Exception e){
				Console.WriteLine("Database query error: " + e.Message);
			}
			return null;
		}

		public string GetTemperature(string query) {
			try{
				using (conn = new NpgsqlConnection(connString)){
					conn.Open();
					using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn)){
						NpgsqlDataReader reader = cmd.ExecuteReader();
						string result = "Invalid";
						if (reader.HasRows){
							while (reader.Read()){
								result = reader[0] + "\n";
							}
						}
						return result;
					}
				}
			}
			catch (Exception e){
				return(e.Message);
			}
		}
	}
}
