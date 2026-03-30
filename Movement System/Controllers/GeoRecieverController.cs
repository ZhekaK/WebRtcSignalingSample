using DataType.ExternalSystems.AR_HUD;
using System;
using UnityEngine;

namespace Movement.Controllers
{
    /// <summary> The controller of recieve geo movement in WorldSpace </summary> 
    public class GeoRecieverController : AbstractMovementController
    {
        public GeoRecieverController(MovementMover mover)
        {
            ID = mover.ID;
            UdpReceiver.Instance.OnReceiveCoordinatesData += RecieveData;
        }

        public void RecieveData(object sender, ReceiveDataEventArgs e)
        {
            AR_HUD_VISUAL_PARAMS geoTransform = new();
            try
            {
                geoTransform = UtilitySerialization.ByteArrayToStruct<AR_HUD_VISUAL_PARAMS>(e.Bytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=orange>{ex}</color>");
                return;
            }

            _unityTransform = UtilityGeoMathWrapper.Instance.GetUnityTransformFromGeo(geoTransform);

            // Update other systems data:
            AirportsManager.Instance.Lat_deg = geoTransform.AcLatitude_rad * Mathf.Rad2Deg;
            AirportsManager.Instance.Lon_deg = geoTransform.AcLongitude_rad * Mathf.Rad2Deg;
        }

        public override void Dispose()
        {
            UdpReceiver.Instance.OnReceiveCoordinatesData -= RecieveData;
        }
    }
}