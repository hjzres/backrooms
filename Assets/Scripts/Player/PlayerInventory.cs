using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Local inventory for the owning player. Toggled with Tab; while open the
// cursor is released and PlayerCam/PlayerMovement suspend look/move input
// by checking LocalUiOpen.
public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 8;

    public static PlayerInventory Local { get; private set; }
    public static bool LocalUiOpen { get; private set; }
    public static event Action<bool> UiToggled;

    public event Action Changed;

    readonly List<string> _items = new List<string>();
    public IReadOnlyList<string> Items => _items;

    PlayerInput _playerInput;
    InputAction _inventoryAction;

    public bool AddItem(string item)
    {
        if (_items.Count >= SlotCount) return false;
        _items.Add(item);
        Changed?.Invoke();
        return true;
    }

    public bool RemoveItem(string item)
    {
        if (!_items.Remove(item)) return false;
        Changed?.Invoke();
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Local = this;

        _playerInput = GetComponent<PlayerInput>();
        _playerInput.enabled = true;
        _inventoryAction = _playerInput.actions["Inventory"];
        _inventoryAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this)
        {
            SetUiOpen(false);
            Local = null;
        }
    }

    void OnDisable()
    {
        if (_inventoryAction != null) _inventoryAction.Disable();
    }

    void Update()
    {
        if (_inventoryAction == null) return;

        if (_inventoryAction.WasPerformedThisFrame())
            SetUiOpen(!LocalUiOpen);
    }

    static void SetUiOpen(bool open)
    {
        if (LocalUiOpen == open) return;
        LocalUiOpen = open;

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        UiToggled?.Invoke(open);
    }
}
