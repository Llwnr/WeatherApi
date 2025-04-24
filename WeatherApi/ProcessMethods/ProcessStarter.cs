using WeatherApi.Helper;

namespace WeatherApi.ProcessMethods;

public class ProcessStarter{
    private readonly PostgreSqlService _postgreSqlService;
    public ProcessStarter(PostgreSqlService dbService){
        // PostgreSqlService dbService = new PostgreSqlService(builder.Configuration.GetConnectionString("PostgresqlConnection"));
        GeospatialProcessingService geoService = new GeospatialProcessingService(dbService);
        // try{
        //     await geoService.DownloadAndProcessBatch();
        // }
        // catch (Exception e){
        //     Console.WriteLine("GeoService Error: " + e.Message);
        // }
        _postgreSqlService = dbService;
        Console.WriteLine("Done");
    }
}