using UnityEngine;
using UnityEngine.UI;

namespace Movement.Controllers
{
    /// <summary> The controller of debug geo movement in WorldSpace </summary> 
    public class GeoDebugController : AbstractMovementController
    {
        private readonly InputField _inputFieldLat;
        private readonly InputField _inputFieldLon;
        private readonly InputField _inputFieldAlt;
        private const float LOOK_SENSIVITY = 100f;

        public GeoDebugController(MovementMover mover)
        {
            ID = mover.ID;
            _unityTransform.position = mover.transform.position;
            _unityTransform.rotation = mover.transform.rotation;

#warning  ак то получить ссылки на Debug пол€ координат
            //_inputFieldLat = null;
            //_inputFieldLon = null;
            //_inputFieldAlt = null;
        }

        private bool IsValid => _inputFieldLat && _inputFieldLon && _inputFieldAlt;


        public override void UpdateMovers()
        {
            if (!IsValid) return;

            if (UseMovement())
            {
                CalculatePosition();
                CalculateRotation();
            }

            base.UpdateMovers();
        }

        protected override void CalculatePosition()
        {
            _unityTransform.position = UtilityGeoMathWrapper.Instance.GetUnityPositionFromGeo(double.Parse(_inputFieldLat.text) * Mathf.Deg2Rad,
                                                                                              double.Parse(_inputFieldLon.text) * Mathf.Deg2Rad,
                                                                                              double.Parse(_inputFieldAlt.text) * Mathf.Deg2Rad);
        }

        protected override void CalculateRotation()
        {
            float horizontal = -Input.GetAxis("Mouse Y") * LOOK_SENSIVITY * Time.deltaTime;
            float vertical = Input.GetAxis("Mouse X") * LOOK_SENSIVITY * Time.deltaTime;
            _unityTransform.rotation = Quaternion.Euler(_unityTransform.rotation.eulerAngles.x + horizontal, _unityTransform.rotation.eulerAngles.y + vertical, 0);
        }

        private bool UseMovement()
        {
            if (Input.GetKey(KeyCode.Mouse1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return false;
        }
    }
}