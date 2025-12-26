using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerCam : MonoBehaviour
    {
        [SerializeField] Camera cam;
        private float sens;

        private float xRotation;
        private float yRotation;
        [SerializeField] InputActionReference lookaction;
        private InputAction look;
        void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            look = lookaction.action;
            look.Enable();
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
            // float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sens;
            // float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sens;

            // yRotation += mouseX;
            // xRotation -= mouseY;
            // xRotation = Math.Clamp(xRotation, -90f, 90f);

            // transform.rotation = Quaternion.Euler(0, yRotation, 0);

            Vector2 looking = look.ReadValue<Vector2>();
            if(looking != Vector2.zero)
            Debug.Log(looking);
        }
    }
}