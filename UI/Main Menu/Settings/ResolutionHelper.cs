using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ResolutionHelper
{
    /// <summary>
    /// Getting a sorted array with all available screen resolutions 
    /// </summary>
    /// <returns></returns>
    public static Resolution[] GetResolutions()
    {
        float screenAspectRation = (float)(Screen.mainWindowDisplayInfo.width) / (float)(Screen.mainWindowDisplayInfo.height);
        if (Display.displays.Length > 1)
        {
            for (int i = 1; i < Display.displays.Length; i++)
            {
                if (Mathf.Abs(screenAspectRation - ((float)Display.displays[i].systemWidth / (float)Display.displays[i].systemHeight)) > 0.01f)
                {
                    Resolution maxMainDisplayResolution = new Resolution();
                    maxMainDisplayResolution.width = Display.displays[0].systemWidth;
                    maxMainDisplayResolution.height = Display.displays[0].systemHeight;

                    Resolution[] resolutions = new[] { maxMainDisplayResolution };
                    return resolutions;
                }
            }
        }

        Resolution[] allResolutions = Screen.resolutions
                                        .GroupBy(r => (r.width, r.height, r.refreshRate))
                                        .Select(g => g.First())
                                        .OrderByDescending(r => r.width)
                                        .ThenByDescending(r => r.height)
                                        .ThenByDescending(r => r.refreshRate)
                                        .ToArray();
        var result = new List<Resolution>();

        foreach (var resolution in allResolutions)
        {
            if (Mathf.Abs((float)resolution.width / (float)resolution.height - screenAspectRation) < 0.01f)
            {
                result.Add(resolution);
            }
        }

        return result.ToArray();

    }

    public static string ResolutionToString(Resolution resolution)
    {
        return $"{resolution.width}x{resolution.height} ({resolution.refreshRate}Hz)";
    }

    /// <summary>
    /// Finding the index of the current screen resolution in an array
    /// </summary>
    /// <param name="resolutions"></param>
    /// <returns></returns>
    public static int FindCurrentResolutionIndex(Resolution[] resolutions)
    {
        var current = Screen.currentResolution;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == current.width &&
                resolutions[i].height == current.height &&
                resolutions[i].refreshRate == current.refreshRate)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Search index for the closest suitable screen resolution
    /// </summary>
    /// <param name="resolutions"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="refreshRate"></param>
    /// <returns></returns>
    public static int FindClosestResolutionIndex(Resolution[] resolutions, int width, int height, int refreshRate)
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < resolutions.Length; i++)
        {
            var res = resolutions[i];
            float distance = Mathf.Abs(res.width - width) +
                           Mathf.Abs(res.height - height) +
                           Mathf.Abs(res.refreshRate - refreshRate) / 10f;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}