using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class WeatherVolumesModule : BaseWeatherModule
{
    [Header("- - Post Process Volume:")]
    [SerializeField, DisableEdit] private Volume _generalVolume;
    [SerializeField, DisableEdit] private Volume _skyVolume;
    [SerializeField, DisableEdit] private Volume _cloudsVolume;
    [SerializeField, DisableEdit] private Volume _fogVolume;
    [SerializeField, DisableEdit] private Volume _postProcessVolume;

    private const string GENERAL_VOLUME_NAME = "General Volume";
    private const string SKY_AND_WIND_VOLUME_NAME = "Sky Volume";
    private const string CLOUDS_VOLUME_NAME = "Clouds Volume";
    private const string FOG_VOLUME_NAME = "Fog Volume";
    private const string POST_PROCESS_VOLUME_NAME = "Post Process Volume";

    public override void Initialize()
    {
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);

        foreach (Volume volume in volumes)
        {
            if (volume.name == GENERAL_VOLUME_NAME) _generalVolume = volume;
            if (volume.name == SKY_AND_WIND_VOLUME_NAME) _skyVolume = volume;
            if (volume.name == CLOUDS_VOLUME_NAME) _cloudsVolume = volume;
            if (volume.name == FOG_VOLUME_NAME) _fogVolume = volume;
            if (volume.name == POST_PROCESS_VOLUME_NAME) _postProcessVolume = volume;
        }

        IsModuleValid = true;
        IsModuleValid &= _generalVolume;
        IsModuleValid &= _skyVolume;
        IsModuleValid &= _cloudsVolume;
        IsModuleValid &= _fogVolume;
        IsModuleValid &= _postProcessVolume;
    }

    public override void SetWeatherProfile(WeatherProfile weatherProfile)
    {
        if (!IsModuleValid) return;

        _generalVolume.sharedProfile = weatherProfile.GeneralVolume;
        _generalVolume.profile = weatherProfile.GeneralVolume;

        _skyVolume.sharedProfile = weatherProfile.SkyVolume;
        _skyVolume.profile = weatherProfile.SkyVolume;

        _cloudsVolume.sharedProfile = weatherProfile.CloudsVolume;
        _cloudsVolume.profile = weatherProfile.CloudsVolume;

        _fogVolume.sharedProfile = weatherProfile.FogVolume;
        _fogVolume.profile = weatherProfile.FogVolume;

        _postProcessVolume.sharedProfile = weatherProfile.PostProcessVolume;
        _postProcessVolume.profile = weatherProfile.PostProcessVolume;
    }
}