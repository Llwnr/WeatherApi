using Microsoft.Extensions.Configuration;
using Npgsql.Replication.TestDecoding;
using WeatherApi.Helper;
using WeatherApi.ProcessMethods;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(new PostgreSqlService(builder.Configuration.GetConnectionString("PostgresqlConnection")));
UrlService.SetConfiguration(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

PostgreSqlService dbService = new PostgreSqlService(builder.Configuration.GetConnectionString("PostgresqlConnection"));
GeospatialProcessingService geoService = new GeospatialProcessingService(dbService);
try{
    await geoService.DownloadAndProcessBatch();
}
catch (Exception e){
    Console.WriteLine("GeoService Error: " + e.Message);
}

app.Run();


