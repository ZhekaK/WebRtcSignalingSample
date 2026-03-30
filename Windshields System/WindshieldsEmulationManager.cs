using UnityEngine;
using WindshieldEmulation;
using WindshieldEmulation.HeadPositioningSubsystem;
using WindshieldEmulation.Serialization;

public class WindshieldsEmulationManager : MonoBehaviour
{
    public static WindshieldsEmulationManager Instance { get; private set; }

    public DeserializationResults WindshieldsConfig { get; private set; }

    private WindshieldEmulationSubsystem<Vector3> _subsystem;
    public static readonly string FILE_NAME = "Windshields Settings.json";


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager()
    {
        if (Instance) return;

        GameObject manager = new(nameof(WindshieldsEmulationManager));
        Instance = manager.AddComponent<WindshieldsEmulationManager>();
        DontDestroyOnLoad(manager);

        Instance.WindshieldsConfig = EmulationElementJsonSerializer.Deserialize($"{SaveManager.FOLDER_NAME}/{FILE_NAME}");
        Instance.InitializeSubsystem();
    }

    private void InitializeSubsystem()
    {
        _subsystem = new WindshieldEmulationSubsystem<Vector3>(new HololensHeadPositioningSubsystem(), WindshieldsConfig.FlatWindowEmulatorDataKits);
        _subsystem.SetPOVData(new(0, 0, -0.5f));
    }

    public Vector3 NewPOV;
    [ContextMenu("Set new POV")]
    public void SetNewPOV()
    {
        _subsystem.SetPOVData(NewPOV);
    }

    /// <summary> Add render camera from emulation sybsystem </summary>
    public void AddCameraToSybsystem(Camera renderCamera) => _subsystem.RegisterCamera(renderCamera);

    /// <summary> Remove render camera from emulation sybsystem </summary>
    public void RemoveCameraToSybsystem(Camera renderCamera) => _subsystem.UnregisterCamera(renderCamera);
}