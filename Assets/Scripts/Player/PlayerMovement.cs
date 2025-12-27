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
    private PlayerInputHandler _inputHandler;
    private InputAction _inputAction;

    // Ground Check
    private bool _isGrounded;
    [SerializeField] float playerHeight;
    [SerializeField] LayerMask whatIsGround;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    void OnEnable()
    {
        // _inputAction.Enable();
    }

    void OnDisable()
    {
        // _inputAction.Disable();
    }

    void Update()
    {
        Vector2 inputVector = _inputHandler.Movement.ReadValue<Vector2>();
        _moveDirection = - transform.forward * inputVector.x + transform.right * inputVector.y;

        print(_moveDirection);

        _isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        if (_isGrounded)
            _rb.linearDamping = groundDrag;
        else
            _rb.linearDamping = 0;

        if(_rb.linearVelocity.magnitude > moveSpeed)
        {
            Vector3 limitedVelocity = _rb.linearVelocity.normalized * moveSpeed;
            _rb.linearVelocity = new Vector3(limitedVelocity.x, _rb.linearVelocity.y, limitedVelocity.z);
        }
    }

    void FixedUpdate()
    {
        _rb.AddForce(_moveDirection * 10f * moveSpeed, ForceMode.Force);
    }
}
