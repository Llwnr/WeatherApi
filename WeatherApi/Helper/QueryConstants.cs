namespace WeatherApi.Helper;

public static class QueryConstants{
    public static string GetCurrentHourlyTemperatureData(double lat, double lon, DateTime currentDate){
        int currDay = currentDate.Day;
        return $@"SELECT extract(hour from time) as time, (ST_VALUE(rast, 2, ST_MakePoint({lon}, {lat}))) as value
        FROM public.weather_raster
        WHERE ST_Intersects(rast, ST_MakePoint({lon}, {lat}))
        AND EXTRACT(day from time) = {currDay}
        AND EXTRACT(month from time) = {currentDate.Month}
        AND EXTRACT(year from time) = {currentDate.Year}
        ORDER BY time;";
    }

    public static string GetDailyAverageTemperatureDataOfAWeek(double lat, double lon, DateTime currentDate){
        DateTime daySevenDaysAgo = currentDate.AddDays(-7);
        //Convert to PostGIS datetime format
        string fromDate = daySevenDaysAgo.ToString("yyyy-MM-dd 23:59:59");
        string toDate = currentDate.ToString("yyyy-MM-dd 23:59:59");
        return $@"SELECT TO_CHAR(time, 'MM-dd') AS time, avg(ST_VALUE(rast, 2, ST_MakePoint({lon}, {lat}))) as value
        FROM public.weather_raster
        WHERE ST_Intersects(rast, ST_MakePoint({lon}, {lat}))
        AND time BETWEEN '{fromDate}' AND '{toDate}'
        GROUP BY TO_CHAR(time, 'MM-dd')
        ORDER BY TO_CHAR(time, 'MM-dd');";
    }
    
    public static string GetDailyAverageRain(double lat, double lon, DateTime currentDate){
        DateTime daySevenDaysAgo = currentDate.AddDays(-7);
        //Convert to PostGIS datetime format
        string fromDate = daySevenDaysAgo.ToString("yyyy-MM-dd 23:59:59");
        string toDate = currentDate.ToString("yyyy-MM-dd 23:59:59");
        return $@"select TO_CHAR(time, 'MM-dd') AS time, round(avg(st_value(rast, 3, st_makepoint({lon}, {lat})))::numeric, 6)*3750 as value
        FROM weather_raster
        WHERE st_intersects(rast, st_makepoint({lon}, {lat}))
        AND time between '{fromDate}' AND '{toDate}'
        GROUP BY TO_CHAR(time, 'MM-dd')
        ORDER BY TO_CHAR(time, 'MM-dd')";
    }
    
    public static string GetTotalRainAmount(double lat, double lon, DateTime currentDate){
        return $@"select EXTRACT(hour from time) as time, round(st_value(rast, 3, st_makepoint({lon}, {lat}))::numeric, 5)*3750 as value
        FROM weather_raster
        WHERE st_intersects(rast, st_makepoint({lon}, {lat}))
        AND EXTRACT(day from time) = {currentDate.Day}
        AND EXTRACT(month from time) = {currentDate.Month}
        AND EXTRACT(year from time) = {currentDate.Year}
        ORDER BY time;";
    }
}