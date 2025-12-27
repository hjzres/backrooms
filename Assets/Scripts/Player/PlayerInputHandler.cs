using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public PlayerInput playerInput;

    public InputAction Movement => playerInput.actions["Movement"];
    public InputAction Camera => playerInput.actions["Camera"];

}
