using UnityEngine;

[System.Serializable]
public struct SettingsData
{
    public bool verticalSync;
    public int resolutionIndex;
    public int fullScreenModeIndex;

    public static SettingsData Default()
    {
        return new SettingsData
        {
            verticalSync = true,
            resolutionIndex = -1, // -1 meens "current resolution"
            fullScreenModeIndex = (int)FullScreenMode.ExclusiveFullScreen
        };
    }
}