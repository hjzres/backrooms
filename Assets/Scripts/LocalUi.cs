using System;
using UnityEngine;

// Tracks which local gameplay UI (inventory / pause menu) is open so player
// input scripts can suspend look and movement, and keeps the cursor lock in
// sync. Purely local state — nothing here is networked.
public static class LocalUi
{
    public static bool InventoryOpen { get; private set; }
    public static bool PauseOpen { get; private set; }
    public static bool AnyOpen => InventoryOpen || PauseOpen;

    public static event Action<bool> InventoryToggled;
    public static event Action<bool> PauseToggled;

    public static void SetInventory(bool open)
    {
        if (InventoryOpen == open) return;
        InventoryOpen = open;
        ApplyCursor();
        InventoryToggled?.Invoke(open);
    }

    public static void SetPause(bool open)
    {
        if (PauseOpen == open) return;
        PauseOpen = open;
        ApplyCursor();
        PauseToggled?.Invoke(open);
    }

    // For leaving the session: clears state and frees the cursor for menus.
    public static void Clear()
    {
        InventoryOpen = false;
        PauseOpen = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void ApplyCursor()
    {
        bool free = AnyOpen;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }
}
