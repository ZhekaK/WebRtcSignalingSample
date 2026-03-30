using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[CreateAssetMenu(fileName = "Weather Data SO", menuName = "Scriptable Objects/Weather System/Create Weather Data SO")]
public class WeatherData: ScriptableObject
{
    [field: SerializeField] public List<WeatherProfile> WeatherProfiles { get; private set; }

    public WeatherProfile GetWeatherProfile(WeatherCondition weatherCondition) => WeatherProfiles.Find(profile => profile.WeatherCondition == weatherCondition);


#if UNITY_EDITOR

    private void OnValidate()
    {
        SortWeatherProfiles();
    }

    private void SortWeatherProfiles() { WeatherProfiles = WeatherProfiles.OrderBy(x => x.WeatherCondition).ToList(); }

#endif
}