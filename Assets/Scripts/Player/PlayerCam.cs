using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerCam : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] private float sens;

        private float xRotation;
        private float yRotation;
        [SerializeField] InputActionReference lookaction;
        private InputAction look;
        void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            look = lookaction.action;
        }

        void OnEnable()
        {
            look.Enable();
        }

        void OnDisable()
        {
            look.Disable();
        }

        void Update()
        {
            Vector2 looking = look.ReadValue<Vector2>();

            float mouseX = looking.x * Time.deltaTime * sens;
            float mouseY = looking.y * Time.deltaTime * sens;

            yRotation += mouseX;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        }
    }
}