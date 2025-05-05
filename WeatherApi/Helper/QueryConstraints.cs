namespace WeatherApi.Helper;

public static class QueryConstraints{
    public static string GetCurrentHourlyTemperatureData(double lat, double lon, DateTime currentDate){
        int currDay = currentDate.Day;
        return $@"SELECT extract(hour from time) as time, (ST_VALUE(rast, 2, ST_MakePoint({lon}, {lat}))) as value
        FROM public.weather_raster
        WHERE ST_Intersects(rast, ST_MakePoint({lon}, {lat}))
        AND EXTRACT(day from time) = {currDay}
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
    
    public static string GetTotalRainAmount(double lat, double lon, DateTime currentDate){
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
}