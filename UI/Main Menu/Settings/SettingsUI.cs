using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Dropdown screenModeDropdown;

    private Resolution[] _availableResolutions;
    private bool _isInitializing;

    private void OnEnable()
    {
        SettingsManager.OnSettingsChanged += UpdateUI;
    }

    private void OnDisable()
    {
        SettingsManager.OnSettingsChanged -= UpdateUI;
    }

    private void Start()
    {
        InitializeUI();
        UpdateUI(SettingsManager.GetCurrentSettings());
    }

    private void InitializeUI()
    {
        _isInitializing = true;

        _availableResolutions = SettingsManager.GetAvailableResolutions();
        resolutionDropdown.ClearOptions();

        var resolutionOptions = new List<string>();
        foreach (var resolution in _availableResolutions)
        {
            resolutionOptions.Add(ResolutionHelper.ResolutionToString(resolution));
        }
        resolutionDropdown.AddOptions(resolutionOptions);

        screenModeDropdown.ClearOptions();

        if (Display.displays.Length > 1) 
        {
            screenModeDropdown.AddOptions(new List<string>
            {
                "Полноэкранный"
            });
        }
        else
        {
            screenModeDropdown.AddOptions(new List<string>
            {
                "Полноэкранный",
                "Оконный без рамок",
                "Оконный"
            });
        }

        // Подписка на события
        vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
        _isInitializing = false;
    }

    private void UpdateUI(SettingsData settings)
    {
        if (_isInitializing) return;

        vSyncToggle.SetIsOnWithoutNotify(settings.verticalSync);

        if (settings.resolutionIndex >= 0 && settings.resolutionIndex < _availableResolutions.Length)
        {
            resolutionDropdown.SetValueWithoutNotify(settings.resolutionIndex);
        }
        else
        {
            var currentIndex = ResolutionHelper.FindCurrentResolutionIndex(_availableResolutions);
            resolutionDropdown.SetValueWithoutNotify(currentIndex >= 0 ? currentIndex : 0);
        }

        if ((FullScreenMode)settings.fullScreenModeIndex == FullScreenMode.Windowed)
        {
            screenModeDropdown.SetValueWithoutNotify(screenModeDropdown.options.Count - 1);
        }
        else
        {
            screenModeDropdown.SetValueWithoutNotify(settings.fullScreenModeIndex);
        }

    }
    private void OnVSyncChanged(bool value)
    {
        var settings = SettingsManager.GetCurrentSettings();
        settings.verticalSync = value;
        SettingsManager.SaveSettings(settings);
    }

    private void OnResolutionChanged(int index)
    {
        var settings = SettingsManager.GetCurrentSettings();
        settings.resolutionIndex = index;
        SettingsManager.SaveSettings(settings);
    }

    private void OnScreenModeChanged(int modeIndex)
    {
        var settings = SettingsManager.GetCurrentSettings();
        settings.fullScreenModeIndex = modeIndex;
        SettingsManager.SaveSettings(settings);
    }
}