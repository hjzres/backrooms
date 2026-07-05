using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerCam : NetworkBehaviour
    {
        public const string SensPrefKey = "MouseSensitivity";

        [SerializeField] Camera cam;
        [SerializeField] private float sens = 5f;

        public float Sensitivity
        {
            get => sens;
            set => sens = value;
        }

        [Header("Feel")]
        [SerializeField] float standHeight = 0.497f;
        [SerializeField] float crouchHeight = 0.05f;
        [SerializeField] float baseFov = 60f;
        [SerializeField] float sprintFov = 68f;
        [SerializeField] float bobFrequency = 9f;
        [SerializeField] float bobAmplitude = 0.035f;

        private float _xRotation, _yRotation;
        private float _camHeight;
        private float _bobTimer;
        private PlayerInput _playerInput;
        private InputAction _deltaMouse;
        private PlayerMovement _movement;
        private Rigidbody _rb;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                if (cam != null)
                    cam.gameObject.SetActive(false);

                AudioListener listener = GetComponentInChildren<AudioListener>(true);
                if (listener != null)
                    listener.enabled = false;

                enabled = false;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _movement = GetComponent<PlayerMovement>();
            _rb = GetComponent<Rigidbody>();
            _camHeight = standHeight;
            _yRotation = transform.eulerAngles.y;
            sens = PlayerPrefs.GetFloat(SensPrefKey, sens);

            _playerInput = GetComponent<PlayerInput>();
            _playerInput.enabled = true;
            _deltaMouse = _playerInput.actions["Camera"];
            _deltaMouse.Enable();

            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
                cam.fieldOfView = baseFov;
            }
        }

        void OnDisable()
        {
            if (_deltaMouse != null)
                _deltaMouse.Disable();
        }

        void Update()
        {
            if (_deltaMouse == null || cam == null)
                return;

            if (!LocalUi.AnyOpen)
            {
                Vector2 mouse = _deltaMouse.ReadValue<Vector2>();

                // Mouse deltas are already per-frame; scaling by deltaTime would
                // make sensitivity depend on frame rate.
                _yRotation += mouse.x * sens * 0.02f;
                _xRotation = Mathf.Clamp(_xRotation - mouse.y * sens * 0.02f, -90f, 90f);
            }

            transform.rotation = Quaternion.Euler(0, _yRotation, 0);
            cam.transform.rotation = Quaternion.Euler(_xRotation, _yRotation + 90f, 0);

            UpdateCameraFeel();
        }

        void UpdateCameraFeel()
        {
            if (cam == null || _movement == null)
                return;

            float targetHeight = _movement.IsCrouching ? crouchHeight : standHeight;
            _camHeight = Mathf.Lerp(_camHeight, targetHeight, 12f * Time.deltaTime);

            float speed = 0f;
            if (_rb != null)
                speed = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;

            float bob = 0f;
            if (_movement.IsGrounded && speed > 0.5f)
            {
                _bobTimer += Time.deltaTime * bobFrequency * Mathf.Clamp(speed / 5f, 0.6f, 1.8f);
                bob = Mathf.Sin(_bobTimer) * bobAmplitude;
            }
            else
            {
                _bobTimer = 0f;
            }

            cam.transform.localPosition = new Vector3(0f, _camHeight + bob, 0f);

            float targetFov = _movement.IsSprinting ? sprintFov : baseFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 8f * Time.deltaTime);
        }
    }
}
