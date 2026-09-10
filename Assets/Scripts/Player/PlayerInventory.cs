using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Local inventory for the owning player. Toggled with Tab; while open the
// cursor is released and PlayerCam/PlayerMovement suspend look/move input
// via LocalUi.
//
// Items live in two fixed-size containers: the backpack (the Tab screen) and
// the hotbar (always on the HUD). Slots keep their index, so an empty slot is
// a null entry rather than a gap in a list — that is what lets a specific
// backpack slot be swapped with a specific hotbar slot.
public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 8;
    public const int HotbarSlotCount = 4;

    public enum Container { Backpack, Hotbar }

    public static PlayerInventory Local { get; private set; }

    public event Action Changed;

    readonly string[] _backpack = new string[SlotCount];
    readonly string[] _hotbar = new string[HotbarSlotCount];

    public IReadOnlyList<string> Items => _backpack;
    public IReadOnlyList<string> HotbarItems => _hotbar;

    PlayerInput _playerInput;
    InputAction _inventoryAction;

    // Filled backpack slots, for the "used/total" readout.
    public int UsedSlots
    {
        get
        {
            int used = 0;
            foreach (string item in _backpack)
                if (item != null) used++;
            return used;
        }
    }

    public string GetSlot(Container container, int index)
    {
        string[] slots = SlotsFor(container);
        return index >= 0 && index < slots.Length ? slots[index] : null;
    }

    // Picks up into the first free backpack slot, spilling into the hotbar only
    // once the backpack is full.
    public bool AddItem(string item)
    {
        if (item == null) return false;
        if (!PlaceInFirstEmpty(_backpack, item) && !PlaceInFirstEmpty(_hotbar, item))
            return false;

        Changed?.Invoke();
        return true;
    }

    public bool RemoveItem(string item)
    {
        if (!ClearFirstMatch(_backpack, item) && !ClearFirstMatch(_hotbar, item))
            return false;

        Changed?.Invoke();
        return true;
    }

    // Exchanges the contents of two slots; either or both may be empty.
    public bool Swap(Container fromContainer, int fromIndex, Container toContainer, int toIndex)
    {
        string[] from = SlotsFor(fromContainer);
        string[] to = SlotsFor(toContainer);

        if (fromIndex < 0 || fromIndex >= from.Length) return false;
        if (toIndex < 0 || toIndex >= to.Length) return false;
        if (from == to && fromIndex == toIndex) return false;
        if (from[fromIndex] == null && to[toIndex] == null) return false;

        (from[fromIndex], to[toIndex]) = (to[toIndex], from[fromIndex]);
        Changed?.Invoke();
        return true;
    }

    string[] SlotsFor(Container container) => container == Container.Hotbar ? _hotbar : _backpack;

    static bool PlaceInFirstEmpty(string[] slots, string item)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) continue;
            slots[i] = item;
            return true;
        }

        return false;
    }

    static bool ClearFirstMatch(string[] slots, string item)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != item) continue;
            slots[i] = null;
            return true;
        }

        return false;
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
            LocalUi.SetInventory(false);
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

        // The pause menu takes priority over the inventory.
        if (_inventoryAction.WasPerformedThisFrame() && !LocalUi.PauseOpen)
            LocalUi.SetInventory(!LocalUi.InventoryOpen);
    }

#if UNITY_EDITOR
    // Nothing spawns pickups yet, so this fills a slot from the inspector to
    // try the hotbar swapping. Delete once real items exist.
    [NaughtyAttributes.Button("Add Test Item")]
    void AddTestItem()
    {
        if (!AddItem($"ITEM {UsedSlots + 1}"))
            Debug.Log("Inventory and hotbar are both full.");
    }
#endif
}
