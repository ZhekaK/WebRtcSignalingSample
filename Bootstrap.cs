using GeographicMath.Models;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class Bootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BeforeSceneLoad()
    {
        // Initialize Bootstrap:
        GameObject gameObject = new("Bootstrap Systems");
        gameObject.AddComponent<Bootstrap>();
        gameObject.AddComponent<EventSystem>();
        gameObject.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(gameObject);

        // Load resources:
        DisplaysConfig displayConfig = Resources.Load<DisplaysConfig>("Displays Config");
        AirportsData airportsData = Resources.Load<AirportsData>("Airports Data");
        List<MultilayerSetupStrategy> setupStrategies = Resources.LoadAll<MultilayerSetupStrategy>("Multilayer System/Setup Strategies").ToList();

        // Initialize Managers and Systems with priority:
        /*1*/ SaveManager.InitializeManager();
        /*2*/ DisplaysManager.InitializeManager(displayConfig);
        /*2*/ WindshieldsEmulationManager.InitializeManager();
        /*2*/ UtilityGeoMathWrapper.Initialize(new EllipsoidModel());
        /*3*/ AirportsManager.InitializeManager(airportsData);
        /*4*/ UdpReceiver.InitializeReceiver();
        /*4*/ LayerDataReceiverModeController.InitializeController();
        /*5*/ MovementManager.InitializeManager();
        /*5*/ MultilayerSceneManager.InitializeManager(setupStrategies);
        /*5*/ MultilayerOverlayManager.InitializeManager();
        /*5*/ PipelineManager.InitializeManager();
        /*6*/ SenderManager.InitializeManager();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoad()
    {
        AirportsManager.SetupManager();
        CommandLineManager.UseCommandLineArguments();
    }
}