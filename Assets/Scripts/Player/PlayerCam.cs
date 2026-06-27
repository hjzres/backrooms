using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerCam : NetworkBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] private float sens;

        private float _xRotation, _yRotation;
        private PlayerInput _playerInput;
        private InputAction _deltaMouse;

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

            _playerInput = GetComponent<PlayerInput>();
            _playerInput.enabled = true;
            _deltaMouse = _playerInput.actions["Camera"];
            _deltaMouse.Enable();

            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
            }
        }

        void OnDisable()
        {
            if (_deltaMouse != null)
                _deltaMouse.Disable();
        }

        void Update()
        {
            Vector2 mouse = _deltaMouse.ReadValue<Vector2>();

            float mouseX = mouse.x * Time.deltaTime * sens;
            float mouseY = mouse.y * Time.deltaTime * sens;

            _yRotation += mouseX;
            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

            transform.rotation = Quaternion.Euler(0, _yRotation, 0);
            cam.transform.rotation = Quaternion.Euler(_xRotation, _yRotation + 90f, 0);
        }
    }
}
