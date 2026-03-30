using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AirportsManager : MonoBehaviour
{
    public static AirportsManager Instance { get; private set; }

    public event Action OnBeforeSceneLoaded;
    public event Action OnAfterSceneLoaded;
    public event Action OnBeforeSceneUnloadet;
    public event Action OnAfterSceneUnloadet;

    [field: SerializeField] public AirportsData AirportsData { get; private set; }
    [field: SerializeField] public AirportsData.Airport CurrentAirport { get; private set; }
    [field: SerializeField, DisableEdit] public string CurrentSceneName { get; private set; }

    [HideInInspector] public double Lat_deg;
    [HideInInspector] public double Lon_deg;
    private double _prevLat_deg;
    private double _prevLon_deg;
    private bool _inProcess;


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager(AirportsData airportsData)
    {
        if (Instance) return;

        GameObject manager = new(nameof(AirportsManager));
        Instance = manager.AddComponent<AirportsManager>();
        DontDestroyOnLoad(manager);

        if (airportsData)
            Instance.AirportsData = airportsData;
        else
            Debug.LogError($"<color=red>{nameof(AirportsData)} in (Resources) is not found!</color>");
    }

    /// <summary> Setup the manager when using it for the first time </summary>
    public static void SetupManager()
    {
        Instance.SetAirport(SceneManager.GetActiveScene().name);
    }

    private void SetAirport(string sceneName)
    {
        CurrentSceneName = sceneName;
        if (AirportsData.TryGetAirportBySceneName(sceneName, out AirportsData.Airport airport))
        {
            CurrentAirport = airport;
            UtilityGeoMathWrapper.Instance.SetGeoZeroPoint(airport.ZeroGeoPosition.lat_deg, airport.ZeroGeoPosition.lon_deg, airport.ZeroGeoPosition.alt_m);
        }
        else
        {
            Debug.LogWarning($"<color=orange>Scene with name - {sceneName} is not Airport! Set airport zero point was aborted!</color>");
        }
    }

    /// <summary> Is target scene valid (In build + Can be load) </summary>
    public static bool IsSceneValid(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string buildScenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string buildSceneName = System.IO.Path.GetFileNameWithoutExtension(buildScenePath);

                if (buildSceneName == sceneName)
                    return true;
            }
        }

        Debug.LogWarning($"<color=orange>Scene with name - {sceneName} is not found!</color>");
        return false;
    }

    /// <summary> Load scene with target name, returns AsyncOperation to track progress </summary>
    public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Single)
    {
        if (_inProcess) return null;
        if (!IsSceneValid(sceneName)) return null;

        _inProcess = true;
        OnBeforeSceneLoaded?.Invoke();

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
        asyncOp.completed += (op) =>
        {
            SetAirport(sceneName);
            _inProcess = false;
            OnAfterSceneLoaded?.Invoke();
        };

        return asyncOp;
    }

    /// <summary> Unload scene with target name, returns AsyncOperation to track progress </summary>
    public AsyncOperation UnloadSceneAsync(Scene scene)
    {
        if (_inProcess) return null;
        if (scene == null || !scene.isLoaded) return null;

        _inProcess = true;
        OnBeforeSceneUnloadet?.Invoke();

        AsyncOperation asyncOp = SceneManager.UnloadSceneAsync(scene);
        if (asyncOp == null)
        {
            _inProcess = false;
            return null;
        }

        asyncOp.completed += (op) =>
        {
            _inProcess = false;
            OnAfterSceneUnloadet?.Invoke();
        };

        return asyncOp;
    }

    private void Update() => DynamicAirportChange();

    private void DynamicAirportChange()
    {
        // In the Main Scene?
        if (CurrentSceneName == AirportsData.MenuSceneName) return;

        // Are coordinates changed?
        if (Lat_deg == _prevLat_deg && Lon_deg == _prevLon_deg) return;

        // Are current airport has exist?
        if (CurrentAirport == null) return;

        // Are coordinates within airport zone?
        if (IsWithinAirportZone(CurrentAirport)) return;

        // Search airport with current coordinates
        for (int i = 0; i < AirportsData.Airports.Count; i++)
        {
            // Are coordinates within airport zone?
            if (!IsWithinAirportZone(AirportsData.Airports[i])) continue;

            string newSceneName = AirportsData.GetSceneName(AirportsData.Airports[i]);

            // Is the current airport already loaded?
            if (CurrentSceneName != newSceneName)
            {
                LoadSceneAsync(newSceneName);
                break;
            }
        }

        _prevLat_deg = Lat_deg;
        _prevLon_deg = Lon_deg;
    }

    private bool IsWithinAirportZone(AirportsData.Airport airport)
    {
        if (airport == null) return false;

        return Lat_deg <= airport.ZeroGeoPosition.lat_deg + airport.maxLatOffset
            && Lat_deg >= airport.ZeroGeoPosition.lat_deg - airport.maxLatOffset
            && Lon_deg <= airport.ZeroGeoPosition.lon_deg + airport.maxLonOffset
            && Lon_deg >= airport.ZeroGeoPosition.lon_deg - airport.maxLonOffset;
    }

#if UNITY_EDITOR

    [InspectorButton("Reload Current Scene (Single Mode)")]
    public void ReloadScene() => LoadSceneAsync(CurrentSceneName);

    [InspectorButton("Load Main Scene (Single Mode)")]
    public void ExitToMainScene() => LoadSceneAsync(AirportsData.MenuSceneName);

#endif
}