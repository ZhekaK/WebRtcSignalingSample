using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class WeatherManager : MonoBehaviour
{
    public event Action<WeatherProfile> OnChangeWeather;

    [field: SerializeField, DisableEdit] public WeatherData WeatherData { get; private set; }

    [SerializeField] private List<BaseWeatherModule> WeatherModules = new();


    private void Start() 
    {
        foreach (BaseWeatherModule module in WeatherModules)
            module.Initialize();

        SetWeather(EnviromentSettings.WeatherCondition);
    }

    /// <summary> Set new weather by WeatherProfile </summary>
    public void SetWeather(WeatherProfile weatherProfile)
    {
        if (!weatherProfile)
        {
            Debug.LogError("<color=red>WeatherProfile is null!</color>");
            return;
        }

        foreach (BaseWeatherModule module in WeatherModules)
            module.SetWeatherProfile(weatherProfile);

        OnChangeWeather?.Invoke(weatherProfile);
    }

    /// <summary> Set new weather by WeatherCondition </summary>
    public void SetWeather(WeatherCondition weatherСondition)
    {
        SetWeather(WeatherData.GetWeatherProfile(weatherСondition));
    }


#if UNITY_EDITOR

    [Header("- - Debug:")]
    [SerializeField] WeatherCondition _debugWeatherCondition;

    private void OnValidate()
    {
        if (Application.isPlaying) return;

        WeatherData = Resources.Load<WeatherData>("Weather Data SO");
        WeatherModules = GetComponentsInChildren<BaseWeatherModule>().ToList();

        foreach (BaseWeatherModule module in WeatherModules)
            module.Initialize();
    }

    [InspectorButton("Set Weather")]
    private void SetWeatherEditor()
    {
        Undo.RecordObject(this, "Set new weather in editor");
        SetWeather(_debugWeatherCondition);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SetWeather(WeatherCondition.Clear);
    }
#endif
}