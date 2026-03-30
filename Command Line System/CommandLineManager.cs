using System;
using System.Collections.Generic;

public class CommandLineManager
{
    private static readonly Dictionary<string, string> _arguments = new();

    /// <summary> Arguments Example: --SCENE uuee --LAYER lwir --WEATHER rainy --TIME 15 </summary>
    public static void UseCommandLineArguments()
    {
        InitializeArgumentsDictionary();

        if (_arguments.TryGetValue("--TIME", out string time)) SetTime(time);
        if (_arguments.TryGetValue("--WEATHER", out string weather)) SetWeather(weather);
        if (_arguments.TryGetValue("--LAYER", out string layer)) SetLayer(layer);
        if (_arguments.TryGetValue("--SCENE", out string icao)) SetScene(icao);
    }

    private static void InitializeArgumentsDictionary()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        _arguments.Clear();

        for (int i = 1; i < arguments.Length; i++)
        {
            if (arguments[i].StartsWith("--"))
            {
                string key = arguments[i].ToUpper();
                string value = string.Empty;

                if (i + 1 < arguments.Length && !arguments[i + 1].StartsWith("--"))
                {
                    value = arguments[i + 1].ToLower();
                    i++;
                }

                _arguments[key] = value;
            }
        }
    }

    /// <summary> --TIME (0...23) </summary>
    private static void SetTime(string time)
    {
        if (int.TryParse(time, out int hour))
            EnviromentSettings.Time = new TimeSpan(hour, 0, 0);
    }

    /// <summary> --WEATHER (clear; rain; heavyrain; fog; thunderstorm; snowfall) </summary>
    private static void SetWeather(string weather)
    {
        if (Enum.TryParse<WeatherCondition>(weather, true, out var condition))
            EnviromentSettings.WeatherCondition = condition;
    }

    /// <summary> --LAYER (visible; lwir; labels; swir) </summary>
    private static void SetLayer(string renderLayer)
    {
        if (Enum.TryParse<RenderLayer>(renderLayer, true, out var layer))
            EnviromentSettings.ImitatorVisibleLayer = layer;
    }

    /// <summary> --SCENE (urss; uuee; unne) </summary>
    private static void SetScene(string icao)
    {
        if (string.IsNullOrEmpty(icao)) return;

        icao = icao.ToUpper();

        AirportsManager.Instance.LoadSceneAsync(AirportsManager.Instance.AirportsData.GetSceneName(icao));
    }
}