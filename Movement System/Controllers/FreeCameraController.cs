using UnityEngine;

namespace Movement.Controllers
{
    /// <summary> The controller of free movement in WorldSpace </summary> 
    public class FreeCameraController : AbstractMovementController
    {
        private const float BASE_SPEED = 25;
        private const float DASH_SPEED = 125;
        private const float SLOW_SPEED = 5;
        private const float LOOK_SENSIVITY = 100f;

        public FreeCameraController(MovementMover mover)
        {
            ID = mover.ID;
            _unityTransform.position = mover.transform.position;
            _unityTransform.rotation = mover.transform.rotation;
        }

        public override void UpdateMovers()
        {
            if (UseMovement())
            {
                CalculatePosition();
                CalculateRotation();
            }

            base.UpdateMovers();
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

        protected override void CalculatePosition()
        {
            float speed = BASE_SPEED;
            if (Input.GetKey(KeyCode.LeftShift))
                speed = DASH_SPEED;
            else if (Input.GetKey(KeyCode.LeftControl))
                speed = SLOW_SPEED;

            float up = 0;
            if (Input.GetKey(KeyCode.E))
                up = 1f;
            else if (Input.GetKey(KeyCode.Q))
                up = -1f;

            Vector3 frontMul = _unityTransform.rotation * Vector3.forward * Input.GetAxis("Vertical");
            Vector3 rightMul = _unityTransform.rotation * Vector3.right * Input.GetAxis("Horizontal");
            Vector3 upMul = _unityTransform.rotation * Vector3.up * up;

            Vector3 move = Vector3.ClampMagnitude(frontMul + rightMul + upMul, 1);
            _unityTransform.position += speed * Time.deltaTime * move;
        }

        protected override void CalculateRotation()
        {
            float horizontal = -Input.GetAxis("Mouse Y") * LOOK_SENSIVITY * Time.deltaTime;
            float vertical = Input.GetAxis("Mouse X") * LOOK_SENSIVITY * Time.deltaTime;
            _unityTransform.rotation = Quaternion.Euler(_unityTransform.rotation.eulerAngles.x + horizontal, _unityTransform.rotation.eulerAngles.y + vertical, 0);
        }
    }
}