using System;
using UnityEngine;

public class TimeRotator : MonoBehaviour
{
    [SerializeField, Range(-90, 90)] private float _lat_deg = 45f;
    [SerializeField, Range(0, 360)] private float _angleToEast = 45f;

    private const float AXIAL_TILT = 23.5f;
    private const float SECONDS_IN_DAY = 86400f;
    private const int FIXED_DAY_OF_YEAR = 67; // Fixed special day of the year (March 7)

    private void Start()
    {
        UpdateRotation(EnviromentSettings.Time);
    }

    /// <summary> Update sun rotation of time day </summary>
    public void UpdateRotation(TimeSpan time) { transform.eulerAngles = CalculateEulerAngles(time); }

    private Vector3 CalculateEulerAngles(TimeSpan currentTime)
    {
        // 1. Calculation of time parameters:
        float totalSeconds = (float)(currentTime.TotalSeconds + 12 * 3600) % SECONDS_IN_DAY;
        float hourAngle = (totalSeconds / SECONDS_IN_DAY * 360f - 180f); // Часовой угол в градусах

        // 2. Calculation of the declination of the sun (by simplified sinuisudal dependence)
        float solarDeclination = AXIAL_TILT * Mathf.Sin(Mathf.Deg2Rad * (360f * (FIXED_DAY_OF_YEAR - 81) / 365f));

        // 3. Calculating the height of the sun above the horizon
        float elevation = CalculateElevation(hourAngle, solarDeclination);

        // 4. Calculating the azimuth of the sun
        float azimuth = CalculateAzimuth(hourAngle, solarDeclination, elevation);

        // 5. Apply a direction offset to the east
        azimuth = (azimuth + _angleToEast) % 360f;

        return new Vector3(-elevation, azimuth, 0f);
    }

    private float CalculateElevation(float hourAngle, float declination)
    {
        float latRad = Mathf.Deg2Rad * _lat_deg;
        float decRad = Mathf.Deg2Rad * declination;
        float haRad = Mathf.Deg2Rad * hourAngle;

        // Sun height: sin(h) = sin(φ)*sin(δ) + cos(φ)*cos(δ)*cos(H)
        float sinElevation = Mathf.Sin(latRad) * Mathf.Sin(decRad) + Mathf.Cos(latRad) * Mathf.Cos(decRad) * Mathf.Cos(haRad);

        return Mathf.Rad2Deg * Mathf.Asin(sinElevation);
    }

    private float CalculateAzimuth(float hourAngle, float declination, float elevation)
    {
        float latRad = Mathf.Deg2Rad * _lat_deg;
        float decRad = Mathf.Deg2Rad * declination;
        float haRad = Mathf.Deg2Rad * hourAngle;
        float elRad = Mathf.Deg2Rad * elevation;

        if (Mathf.Approximately(elRad, Mathf.PI / 2))
            return 180f;

        float sinAzimuth = -Mathf.Sin(haRad) * Mathf.Cos(decRad) / Mathf.Cos(elRad);
        float cosAzimuth = (Mathf.Sin(decRad) - Mathf.Sin(latRad) * Mathf.Sin(elRad))
                         / (Mathf.Cos(latRad) * Mathf.Cos(elRad));

        float azimuthRad = Mathf.Atan2(sinAzimuth, cosAzimuth);
        float azimuth = Mathf.Rad2Deg * azimuthRad;

        return (azimuth + 360f) % 360f;
    }


#if UNITY_EDITOR

    [Header("- - Debug:")]
    [SerializeField, Range(0, 23)] private int _hours;
    [SerializeField, Range(0, 59)] private int _minutes;

    private void OnValidate()
    {
        //if (Application.isPlaying) return;

        UpdateRotation(new TimeSpan(_hours, _minutes, 0));
    }

#endif
}