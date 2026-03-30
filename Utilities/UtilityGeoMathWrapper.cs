using DataType.ExternalSystems.AR_HUD;
using GeographicMath;
using GeographicMath.Models;
using UnityEngine;

public class UtilityGeoMathWrapper
{
    public static UtilityGeoMathWrapper Instance { get; private set; }

    private readonly IGeodeticModel GeodeticModel; // Default use: new EllipsoidModel()


    /// <summary> Initialize GeoMathWrapper as singleton </summary>
    public static void Initialize(IGeodeticModel geodeticModel)
    {
        Instance = new UtilityGeoMathWrapper(geodeticModel);
    }

    private UtilityGeoMathWrapper(IGeodeticModel geodeticModel) { GeodeticModel = geodeticModel; }

    /// <summary> Set target GeoZeroPoint to GeodeticModel </summary>
    public void SetGeoZeroPoint(double lat0_deg, double lon0_deg, double alt0_m)
    {
        GeodeticModel.SetRefPoint(Utils.DegreesToRadiansLatitude(lat0_deg), Utils.DegreesToRadiansLongitude(lon0_deg), alt0_m);
    }

    /// <summary> Get Unity-coordinates from Geo-coordinates </summary>
    public UnityTransform GetUnityTransformFromGeo(AR_HUD_VISUAL_PARAMS geodetic)
    {
        UnityTransform localPosition = new();

        var (east_m, north_m, up_m) = GeodeticModel.GeodeticToENU(geodetic.AcLatitude_rad, geodetic.AcLongitude_rad, geodetic.GeometricHeightAboveMSL_m);

        localPosition.position = new((float)east_m, (float)up_m, (float)north_m);
        localPosition.rotation = Quaternion.Euler(GetUnityAnglesFromGeo(geodetic));
        localPosition.offset = new(geodetic.CameraExternalCoordinateX, geodetic.CameraExternalCoordinateY, geodetic.CameraExternalCoordinateZ);

        return localPosition;
    }

    /// <summary> Get Unity-position from Geo-position </summary>
    public Vector3 GetUnityPositionFromGeo(double lat_rad, double lon_rad, double alt_m)
    {
        var (east_m, north_m, up_m) = GeodeticModel.GeodeticToENU(lat_rad, lon_rad, alt_m);

        return new Vector3((float)east_m, (float)up_m, (float)north_m);
    }

    /// <summary> Get Unity-rotation from Geo-angles </summary>
    public Vector3 GetUnityAnglesFromGeo(AR_HUD_VISUAL_PARAMS geodetic)
    {
        return GetUnityRotationFromGeo(geodetic).eulerAngles;
    }

    /// <summary> Get Unity-rotation from Geo </summary>
    public Quaternion GetUnityRotationFromGeo(AR_HUD_VISUAL_PARAMS geodetic)
    {
        return Quaternion.Euler(-(float)geodetic.PitchAngle_deg, (float)geodetic.TrueHeading_deg, -(float)geodetic.RollAngle_deg);
    }

    /// <summary> Get Geo-coordinates from Unity-coordinates </summary>
    public AR_HUD_VISUAL_PARAMS GetGeoTransformFromUnity(Vector3 position, Vector3 rotation)
    {
        var (lat_rad, lon_rad, alt_m) = GeodeticModel.ENUToGeodetic(position.x, position.z, position.y);
        return new AR_HUD_VISUAL_PARAMS
        {
            AcLatitude_rad = lat_rad,
            AcLongitude_rad = lon_rad,
            GeometricHeightAboveMSL_m = alt_m,
            RollAngle_deg = -rotation.x,
            TrueHeading_deg = rotation.y,
            PitchAngle_deg = -rotation.z
        };
    }
}