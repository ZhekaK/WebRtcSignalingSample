using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "Weather Profile _", menuName = "Scriptable Objects/Weather System/Create Weather Profile SO")]
public class WeatherProfile : ScriptableObject
{
    [field: SerializeField] public WeatherCondition WeatherCondition { get; private set; }


    [field: Header("- - Lens Effect:")]
    [field: SerializeField] public Material LensEffectMat { get; private set; }


    [field: Header("- - Volume Profiles:")]
    [field: SerializeField] public VolumeProfile GeneralVolume { get; private set; }
    [field: SerializeField] public VolumeProfile SkyVolume { get; private set; }
    [field: SerializeField] public VolumeProfile CloudsVolume { get; private set; }
    [field: SerializeField] public VolumeProfile FogVolume { get; private set; }
    [field: SerializeField] public VolumeProfile PostProcessVolume { get; private set; }


    [Header("- - VFX Graphs:")]
    [SerializeField] private List<GameObject> _VFX;
    public List<GameObject> VFX => _VFX;
}