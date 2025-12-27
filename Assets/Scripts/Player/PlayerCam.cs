using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerCam : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] private float sens;

        private float _xRotation, _yRotation;
        private PlayerInput _playerInput;
        private InputAction _deltaMouse;
        void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _playerInput = GetComponent<PlayerInput>();

            _deltaMouse = _playerInput.actions["Camera"];
        }

        void OnEnable()
        {
            _deltaMouse.Enable();
        }

        void OnDisable()
        {
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