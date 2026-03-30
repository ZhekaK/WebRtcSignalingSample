using UnityEngine;

public abstract class BaseWeatherModule: MonoBehaviour
{
    public bool IsModuleValid { get; protected set; }

    /// <summary> Initialize and validate module </summary>
    public abstract void Initialize();

    /// <summary> Set module parameters by WeatherProfile </summary>
    public abstract void SetWeatherProfile(WeatherProfile weatherProfile);
}