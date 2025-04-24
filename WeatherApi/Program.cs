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

var connString = builder.Configuration.GetConnectionString("PostgresqlConnection");
builder.Services.AddSingleton(sp => new PostgreSqlService(connString));
UrlService.SetConfiguration(builder.Configuration);

builder.Services.AddHostedService<GeospatialProcessingService>();

var MyJsCrossOrigins = "_myLeafletJsOrigins";
builder.Services.AddCors(options => {
    options.AddPolicy(
        name: MyJsCrossOrigins,
        builder => {
            builder.WithOrigins("http://127.0.0.1:5500")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseCors(MyJsCrossOrigins);
app.MapControllers();

app.Run();


