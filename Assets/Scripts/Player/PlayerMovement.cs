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
    [Header("Move")]
    [SerializeField] float moveSpeed;
    [SerializeField] float runSpeedMultiplier;
    [SerializeField] float stamina;
    float Stamina
    {
        get { return stamina; }
        set
        {
            if (value < 0)
            {
                stamina = 0;
            }
            else if (value > _maxStamina)
            {
                stamina = _maxStamina;
            } else {
                stamina = value;
            }
        }
    }
    private float _maxStamina;
    [SerializeField] float staminaIncreaseRate;
    [SerializeField] float staminaDecreaseRate;
    [SerializeField] float staminaCooldown;
    private Vector3 _moveDirection;
    [SerializeField] float groundDrag;

    [Header("Jump")]
    [SerializeField] float jumpForce;
    [SerializeField] float jumpCooldown;
    [SerializeField] float airMultiplier;
    bool _readyToJump;

    // Input
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _runAction;

    [Header("Ground")]
    [SerializeField] float playerHeight;
    [SerializeField] LayerMask whatIsGround;
    private bool _isGrounded;

    void Awake()
    {
        _maxStamina = 100f;
        Stamina = _maxStamina;
        
        _readyToJump = true;

        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        _playerInput = GetComponent<PlayerInput>();

        _moveAction = _playerInput.actions["Movement"];
        _jumpAction = _playerInput.actions["Jump"];
        _runAction = _playerInput.actions["Run"];
    }

    void OnEnable()
    {
        _moveAction.Enable();
        _jumpAction.Enable();
    }

    void OnDisable()
    {
        _moveAction.Disable();
        _jumpAction.Disable();
    }

    void Update()
    {
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        _moveDirection = -transform.forward * _moveAction.ReadValue<Vector2>().x + transform.right * _moveAction.ReadValue<Vector2>().y;

        DragControl();

        if (_jumpAction.IsPressed() && _readyToJump && _isGrounded)
        {
            _readyToJump = false;

            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }

        if (stamina > 0)
            StaminaControl();
        else
            Invoke(nameof(StaminaControl), staminaCooldown);
    }

    void FixedUpdate()
    {
        if (_isGrounded)
            if(_runAction.IsPressed() && Stamina > 0)
                _rb.AddForce(_moveDirection * 10f * moveSpeed * runSpeedMultiplier, ForceMode.Force);
            else
                _rb.AddForce(_moveDirection * 10f * moveSpeed, ForceMode.Force);
        else
            _rb.AddForce(_moveDirection * 10f * moveSpeed * airMultiplier, ForceMode.Force);
    }

    void Jump()
    {
        _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        _rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    void ResetJump()
    {
        _readyToJump = true;
    }

    void DragControl()
    {
        if (_isGrounded)
            _rb.linearDamping = groundDrag;
        else
            _rb.linearDamping = 0;
    }

    void StaminaControl()
    {
        if (_runAction.IsPressed() && Stamina > 0)
        {
            Stamina -= staminaDecreaseRate;
        }
        else
        {
            Stamina += staminaIncreaseRate;
        }
    }
}
