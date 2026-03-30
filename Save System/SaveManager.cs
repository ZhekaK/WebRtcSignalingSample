using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    [field: SerializeField] public Settings Settings { get; private set; } = new Settings();

    public static readonly string FOLDER_NAME = "Configs";
    public static readonly string FILE_NAME = "General Settings.json";


    /// <summary> Initialize manager as singleton </summary>
    public static void InitializeManager()
    {
        if (Instance) return;

        GameObject manager = new(nameof(SaveManager));
        Instance = manager.AddComponent<SaveManager>();
        DontDestroyOnLoad(manager);

        Instance.Load();
    }

    /// <summary> Load settings data from settings file </summary>
    public void Load()
    {
        if (!File.Exists($"{FOLDER_NAME}/{FILE_NAME}")) 
            Save();

        try
        {
            string jsonSettings = File.ReadAllText($"{FOLDER_NAME}/{FILE_NAME}");
            Settings = JsonUtility.FromJson<Settings>(jsonSettings);
            //Debug.Log($"Settings success loadet from: {FOLDER_NAME}/{FILE_NAME}");
        }
        catch
        {
            Debug.LogError("Settings File has not valid data. Check the data!");
        }
    }

    /// <summary> Save settings data to settings file </summary>
    public void Save()
    {
        string jsonSettings = JsonUtility.ToJson(Settings, true);
        File.WriteAllText($"{FOLDER_NAME}/{FILE_NAME}", $"{jsonSettings}");
        Debug.Log($"Settings success saved to: {FOLDER_NAME}/{FILE_NAME}");
    }

    private void OnApplicationQuit() => Save();
}