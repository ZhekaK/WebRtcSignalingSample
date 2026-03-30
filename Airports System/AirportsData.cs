using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Airports Data SO", menuName = "Scriptable Objects/Create Airports Data SO")]
public class AirportsData : ScriptableObject
{
    [field: SerializeField] public string MenuSceneName { get; private set; } = "Main Scene";

    [Serializable]
    public class Airport
    {
        public string icao;
        [Tooltip("Ignore in build?")] public bool ignore;
        [HideIf(nameof(ignore), true)] public string buttonName;
        [HideIf(nameof(ignore), true)] public string sceneNameIntegral;
        [HideIf(nameof(ignore), true)] public string sceneNameSGS;
        [HideIf(nameof(ignore), true)] public GeoPosition ZeroGeoPosition;
        [HideIf(nameof(ignore), true), Min(0)] public double maxLatOffset;
        [HideIf(nameof(ignore), true), Min(0)] public double maxLonOffset;
    }
    [field: SerializeField] public List<Airport> Airports { get; private set; } = new();


    /// <summary> Get airport scene name by project type </summary>
    public static string GetSceneName(Airport airport)
    {
        if (airport == null) return string.Empty;

#if APP_TYPE_INTEGRAL
        return airport.sceneNameIntegral;
#elif APP_TYPE_SGS
        return airport.sceneNameSGS;
#else
        return string.Empty;
#endif
    }

    /// <summary> Get airport scene name by ICAO </summary>
    public string GetSceneName(string icao)
    {
        Airport airport = Airports.Find(airport => airport.icao == icao.ToUpper());
        return GetSceneName(airport);
    }

    /// <summary> Get airport by ICAO </summary>
    public bool TryGetAirportByIcao(string icao, out Airport airport)
    {
        airport = Airports.Find(airport => airport.icao == icao.ToUpper());

        return airport != null && !airport.ignore;
    }

    /// <summary> Get airport by scene name </summary>
    public bool TryGetAirportBySceneName(string sceneName, out Airport airport)
    {
#if APP_TYPE_INTEGRAL
        airport = Airports.Find(airport => airport.sceneNameIntegral == sceneName);
#elif APP_TYPE_SGS
        airport = Airports.Find(airport => airport.sceneNameSGS == sceneName);
#else
        airport = null;
#endif

        return airport != null && !airport.ignore;
    }
}