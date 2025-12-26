using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float moveSpeed;
    private Rigidbody _rb;

    private PlayerInput _playerInput;
    private InputAction _inputAction;
    private Vector3 _moveDirection;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        _playerInput = GetComponent<PlayerInput>();

        _inputAction = _playerInput.actions["Movement"];
    }

    void Update()
    {
        _moveDirection = new Vector3(_inputAction.ReadValue<Vector2>().x, 0, _inputAction.ReadValue<Vector2>().y);

        print(_moveDirection);
        _rb.AddForce(_moveDirection * 10f * moveSpeed, ForceMode.Force);
    }
}
