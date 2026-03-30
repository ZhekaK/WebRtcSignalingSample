using System;
using TabletTypes;

public static class EnviromentSettings
{
    public static RenderLayer ImitatorVisibleLayer = RenderLayer.Visible;
    public static WeatherCondition WeatherCondition = WeatherCondition.Clear;
    public static TimeSpan Time = new(9, 0, 0);

    /// <summary> Get time in string format (hours:minutes) </summary>
    public static string GetTime() => Time.ToString(@"hh\:mm");

    /// <summary> Reset all scene settings </summary>
    public static void ResetSettings()
    {
        ImitatorVisibleLayer = RenderLayer.Visible;
        WeatherCondition = WeatherCondition.Clear;
        Time = new(9, 0, 0);
    }
}