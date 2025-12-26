using Unity.VisualScripting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // Components
    private Rigidbody _rb;

    // Properties
    [SerializeField] float moveSpeed;
    private Vector3 _moveDirection;
    [SerializeField] float groundDrag;

    // Input
    private PlayerInput _playerInput;
    private InputAction _inputAction;

    // Ground Check
    private bool _isGrounded;
    [SerializeField] float playerHeight;
    [SerializeField] LayerMask whatIsGround;

    void Awake()
    {

        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        _playerInput = GetComponent<PlayerInput>();

        _inputAction = _playerInput.actions["Movement"];
    }

    void OnEnable()
    {
        _inputAction.Enable();
    }

    void OnDisable()
    {
        _inputAction.Disable();
    }

    void Update()
    {
        _moveDirection = transform.forward * _inputAction.ReadValue<Vector2>().x + transform.right * _inputAction.ReadValue<Vector2>().y;

        print(_moveDirection);

        // _isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        // if (_isGrounded)
        //     _rb.linearDamping = groundDrag;
        // else
        //     _rb.linearDamping = 0;
    }

    void FixedUpdate()
    {
        _rb.AddForce(_moveDirection * 10f * moveSpeed, ForceMode.Force);
    }
}
