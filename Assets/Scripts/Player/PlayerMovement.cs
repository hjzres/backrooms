using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Move")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float runSpeedMultiplier = 1.6f;
    [SerializeField] float crouchSpeedMultiplier = 0.5f;
    [SerializeField] float groundDrag = 5f;
    [SerializeField] float airMultiplier = 0.4f;

    [Header("Stamina")]
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float staminaDecreaseRate = 20f;
    [SerializeField] float staminaIncreaseRate = 15f;
    [SerializeField] float staminaCooldown = 1f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 6f;
    [SerializeField] float jumpCooldown = 0.25f;

    [Header("Ground")]
    [SerializeField] float playerHeight = 2f;
    [SerializeField] LayerMask whatIsGround;

    public float Stamina01 => maxStamina > 0f ? _stamina / maxStamina : 0f;
    public bool IsSprinting { get; private set; }
    public bool IsGrounded => _isGrounded;
    public bool IsCrouching => _netCrouching.Value;

    // Owner-written so remote clients can squash the model while crouched.
    readonly NetworkVariable<bool> _netCrouching = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    Rigidbody _rb;
    Transform _model;
    Vector3 _moveDirection;
    float _stamina;
    float _lastSprintTime;
    bool _exhausted;
    bool _readyToJump = true;
    bool _isGrounded;

    PlayerInput _playerInput;
    InputAction _moveAction;
    InputAction _jumpAction;
    InputAction _runAction;
    InputAction _crouchAction;

    public override void OnNetworkSpawn()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _model = transform.Find("Model");
        _stamina = maxStamina;

        _netCrouching.OnValueChanged += OnCrouchChanged;
        OnCrouchChanged(false, _netCrouching.Value);

        if (!IsOwner)
        {
            _rb.isKinematic = true;
            enabled = false;
            return;
        }

        _playerInput = GetComponent<PlayerInput>();
        _playerInput.enabled = true;
        _moveAction = _playerInput.actions["Movement"];
        _jumpAction = _playerInput.actions["Jump"];
        _runAction = _playerInput.actions["Run"];
        _crouchAction = _playerInput.actions["Crouch"];
        _moveAction.Enable();
        _jumpAction.Enable();
        _runAction.Enable();
        _crouchAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        _netCrouching.OnValueChanged -= OnCrouchChanged;
    }

    void OnDisable()
    {
        if (_moveAction != null) _moveAction.Disable();
        if (_jumpAction != null) _jumpAction.Disable();
        if (_runAction != null) _runAction.Disable();
        if (_crouchAction != null) _crouchAction.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;

        _isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        ReadInput();
        UpdateStamina();

        _rb.linearDamping = _isGrounded ? groundDrag : 0f;
    }

    void ReadInput()
    {
        if (LocalUi.AnyOpen)
        {
            _moveDirection = Vector3.zero;
            IsSprinting = false;
            return;
        }

        Vector2 input = _moveAction.ReadValue<Vector2>();

        // The camera looks along the body's right axis (see PlayerCam), so the
        // view directions are derived from the body transform accordingly.
        Vector3 viewForward = transform.right;
        Vector3 viewRight = -transform.forward;
        _moveDirection = Vector3.ClampMagnitude(viewForward * input.y + viewRight * input.x, 1f);

        bool wantsCrouch = _crouchAction.IsPressed();
        if (_netCrouching.Value != wantsCrouch)
            _netCrouching.Value = wantsCrouch;

        IsSprinting = _runAction.IsPressed()
            && !_exhausted
            && _stamina > 0f
            && !IsCrouching
            && _isGrounded
            && _moveDirection.sqrMagnitude > 0.01f;

        if (_jumpAction.IsPressed() && _readyToJump && _isGrounded && !IsCrouching)
        {
            _readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    void UpdateStamina()
    {
        if (IsSprinting)
        {
            _stamina = Mathf.Max(0f, _stamina - staminaDecreaseRate * Time.deltaTime);
            _lastSprintTime = Time.time;

            if (_stamina <= 0f)
                _exhausted = true;
        }
        else if (Time.time >= _lastSprintTime + staminaCooldown)
        {
            _stamina = Mathf.Min(maxStamina, _stamina + staminaIncreaseRate * Time.deltaTime);
        }

        // Once winded, sprint stays unavailable until some stamina is back to
        // avoid stuttery on/off sprinting at 0.
        if (_exhausted && _stamina >= maxStamina * 0.3f)
            _exhausted = false;
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        float targetSpeed = TargetSpeed();

        Vector3 force = _moveDirection * (targetSpeed * 10f);
        if (!_isGrounded)
            force *= airMultiplier;

        _rb.AddForce(force, ForceMode.Force);

        // Clamp horizontal velocity so the accumulated force can't push the
        // player past the intended speed.
        Vector3 flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        if (flatVel.magnitude > targetSpeed)
        {
            Vector3 limited = flatVel.normalized * targetSpeed;
            _rb.linearVelocity = new Vector3(limited.x, _rb.linearVelocity.y, limited.z);
        }
    }

    float TargetSpeed()
    {
        if (IsCrouching) return moveSpeed * crouchSpeedMultiplier;
        if (IsSprinting) return moveSpeed * runSpeedMultiplier;
        return moveSpeed;
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

    void OnCrouchChanged(bool previous, bool crouching)
    {
        if (_model == null) return;

        // Squash the model (and its capsule collider) downward so the feet
        // stay planted; runs on every client via the network variable.
        float height = crouching ? 0.65f : 1f;
        _model.localScale = new Vector3(1f, height, 1f);
        _model.localPosition = new Vector3(0f, -(1f - height), 0f);
    }
}
