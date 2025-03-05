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

		public async Task AddGeoTiffToPostGIS(string geoTiffPath, DateTime time, string tableName) {
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
			
			Console.WriteLine("Trying to insert to postgis");
			Console.WriteLine("Time: " + time);
			Process raster2Sql = new Process();
			raster2Sql.StartInfo.FileName = "cmd.exe";
			string append = tableExists ? " -a " : "";
			raster2Sql.StartInfo.Arguments = $"/C raster2pgsql -s 4326 -I -C -M -F {append}{geoTiffPath} public.{tableName}";
			raster2Sql.StartInfo.UseShellExecute = false;
			raster2Sql.StartInfo.RedirectStandardOutput = true;
			
			raster2Sql.Start();
			using (StreamWriter file = new StreamWriter(sqlFileOutput)) {
				await file.WriteAsync(await raster2Sql.StandardOutput.ReadToEndAsync());
			}
			
			string pgsqlCmd = $@"psql -U postgres -h localhost -d weather -p 5432 -f {sqlFileOutput}";
			await ProcessExecution.ExecuteCommand(pgsqlCmd, "livewithme0");

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
