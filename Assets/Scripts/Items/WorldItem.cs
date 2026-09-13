using UnityEngine;

// A pickup lying in the world: a renderer and collider for the interaction
// raycast to hit, plus the name PlayerInventory stores once it is picked up.
//
// Local-only, like PlayerInventory — nothing here is networked yet, so each
// client sees and picks up its own copy of a scene-placed item.
public class WorldItem : MonoBehaviour
{
    [SerializeField] string itemName = "BOX";

    // Shown in the HUD prompt and stored in the inventory slot.
    public string ItemName => itemName;

    // Fails when the backpack and hotbar are both full, which leaves the item
    // on the ground rather than deleting it.
    public bool PickUp(PlayerInventory inventory)
    {
        if (inventory == null || !inventory.AddItem(itemName))
            return false;

        Destroy(gameObject);
        return true;
    }
}
