using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using WeatherApi.ProcessMethods;

namespace WeatherApi.Helper {
	public class PostgreSqlService {
		private readonly string _connString;

		public PostgreSqlService(string connString) {
			_connString = connString;
		}

		public async Task AddGeoTiffToPostGis(string inputFilePath, DateTime time, string tableName){
			byte[] rasterData = File.ReadAllBytes(inputFilePath);
			
			try{
				using (var conn = new NpgsqlConnection(_connString)){
					await conn.OpenAsync();
					// Insert the raster data
					string dateTimeStr = time.ToString("yyyy-MM-dd HH:mm:ss");
					string insertQuery = $@"
		                INSERT INTO public.{tableName} (time, rast)
		                VALUES (@time, ST_FromGDALRaster(@rasterData))";
					using (var cmd = new NpgsqlCommand(insertQuery, conn)){
						cmd.Parameters.AddWithValue("time", NpgsqlTypes.NpgsqlDbType.Timestamp, time.ToLocalTime());
						cmd.Parameters.AddWithValue("rasterData", NpgsqlTypes.NpgsqlDbType.Bytea, rasterData);
						await cmd.ExecuteNonQueryAsync();
					}

					Console.WriteLine($"Inserted GeoTIFF data into {tableName} at {dateTimeStr}");
				}
			}
			catch (Exception ex){
				Console.WriteLine($"Inserting to postGis error: {ex.Message}");
			}
		}

		public async Task<bool> TableExists(string tableName){
			try{
				using (var conn = new NpgsqlConnection(_connString)){
					await conn.OpenAsync();
					string query = $@"
		                SELECT EXISTS(
		                    SELECT FROM information_schema.tables
		                    WHERE table_schema='public'
		                    AND table_name='{tableName}'
		                )";
					using (var cmd = new NpgsqlCommand(query, conn)){
						return (bool)await cmd.ExecuteScalarAsync();
					}
				}
			}
			catch (Exception ex){
				Console.WriteLine($"Error while checking if table {tableName} exists: " + ex.Message);
				return false;
			}
		}

		public async Task EnsureTableExists(string tableName){
			try{
				bool tableExists = await TableExists(tableName);
				// Create table with constraints
				if (tableExists) return;

				string createTableQuery = $@"
                    CREATE TABLE public.{tableName} (
                        rid SERIAL PRIMARY KEY,
                        time TIMESTAMP,
                        rast RASTER
                    );
                    CREATE INDEX {tableName}_rast_idx ON public.{tableName} USING GIST (ST_ConvexHull(rast));";
				await ExecuteSqlCommand(createTableQuery);
				Console.WriteLine("Created table schema");

				// Add raster constraints (e.g., SRID, pixel size consistency)
				string addConstraintsQuery = $"SELECT public.AddRasterConstraints('{tableName}', 'rast');";
				await ExecuteSqlCommand(addConstraintsQuery);
				Console.WriteLine($"Table {tableName} created with constraints.");
			}
			catch (Exception ex){
				Console.WriteLine("Failure to create table: " + ex.Message + ex.StackTrace + ex.Source);
			}
		}

		private async Task ExecuteSqlCommand(string query) {
			using (var conn = new NpgsqlConnection(_connString)) {
				await conn.OpenAsync();
				using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn)) {
					await cmd.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task ExecuteScalar(string query){
			using (var conn = new NpgsqlConnection(_connString)) {
				await conn.OpenAsync();
				using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn)) {
					await cmd.ExecuteScalarAsync();
				}
			}
		}

		public string? GetData(string query){
			try{
				using (var conn = new NpgsqlConnection(_connString)){
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
	}
}
