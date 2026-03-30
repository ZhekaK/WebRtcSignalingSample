using UnityEngine;
using System;

public class SettingsManager : MonoBehaviour
{
    public static event Action<SettingsData> OnSettingsChanged;

    private static SettingsData _currentSettings;
    private static bool _isInitialized;
    private static Resolution[] _cachedResolutions;

    private const string SETTINGS_PREFS_KEY = "Settings";

    private void Awake()
    {
        if (_isInitialized) return;

        _cachedResolutions = ResolutionHelper.GetResolutions();
        LoadSettings();
        _isInitialized = true;
    }

    private static void LoadSettings()
    {
        string savedSettings = PlayerPrefs.GetString(SETTINGS_PREFS_KEY, string.Empty);

        if (string.IsNullOrEmpty(savedSettings) == false)
        {
            try
            {
                _currentSettings = JsonUtility.FromJson<SettingsData>(savedSettings);
            }
            catch (Exception)
            {
                _currentSettings = SettingsData.Default();
            }
        }
        else
        {
            _currentSettings = SettingsData.Default();
        }

        ApplySettings(_currentSettings);
    }
    /// <summary>
    /// Save settings to JSON file
    /// </summary>
    /// <param name="newSettings"></param>
    public static void SaveSettings(SettingsData newSettings)
    {
        _currentSettings = newSettings;

        PlayerPrefs.SetString(SETTINGS_PREFS_KEY, JsonUtility.ToJson(_currentSettings));
        PlayerPrefs.Save();

        ApplySettings(_currentSettings);
        OnSettingsChanged?.Invoke(_currentSettings);
    }

    private static void ApplySettings(SettingsData settings)
    {

        QualitySettings.vSyncCount = settings.verticalSync ? 1 : 0;

        Resolution targetResolution;
        if (settings.resolutionIndex >= 0 && settings.resolutionIndex < _cachedResolutions.Length)
        {
            targetResolution = _cachedResolutions[settings.resolutionIndex];
        }
        else
        {
            targetResolution = Screen.currentResolution;
        }

        Screen.SetResolution(
            targetResolution.width,
            targetResolution.height, 
            ((FullScreenMode)settings.fullScreenModeIndex == FullScreenMode.ExclusiveFullScreen || (FullScreenMode)settings.fullScreenModeIndex == FullScreenMode.MaximizedWindow));

        if (Display.displays.Length == 1)
        {
            Screen.fullScreenMode = (FullScreenMode)settings.fullScreenModeIndex;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            
            for (int i = 1; i < Display.displays.Length; i++)
            {
                if (_cachedResolutions.Length > 1)
                    Display.displays[i].Activate(targetResolution.width, targetResolution.height, Screen.currentResolution.refreshRateRatio);
                else
                    Display.displays[i].Activate(Display.displays[i].systemWidth, Display.displays[i].systemHeight, Screen.currentResolution.refreshRateRatio);
            }
        }
    }

    /// <summary>
    /// Get current project settings
    /// </summary>
    /// <returns></returns>
    public static SettingsData GetCurrentSettings() => _currentSettings;

    /// <summary>
    /// Get all available resolutions
    /// </summary>
    /// <returns></returns>
    public static Resolution[] GetAvailableResolutions() => _cachedResolutions;
}