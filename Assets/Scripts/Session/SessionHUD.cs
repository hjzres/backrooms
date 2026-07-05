using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Session
{
    public class SessionHUD : MonoBehaviour
    {
        Label codeLabel;
        VisualElement staminaBar;
        VisualElement staminaFill;
        VisualElement inventoryOverlay;
        VisualElement inventoryGrid;
        Label inventoryCount;

        PlayerMovement localMovement;
        PlayerInventory subscribedInventory;
        float nextCodePoll;
        bool codeResolved;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            codeLabel = root.Q<Label>("CodeLabel");
            staminaBar = root.Q("StaminaBar");
            staminaFill = root.Q("StaminaFill");
            inventoryOverlay = root.Q("InventoryOverlay");
            inventoryGrid = root.Q("InventoryGrid");
            inventoryCount = root.Q<Label>("InventoryCount");

            PlayerInventory.UiToggled += OnInventoryToggled;
            OnInventoryToggled(PlayerInventory.LocalUiOpen);
        }

        void OnDisable()
        {
            PlayerInventory.UiToggled -= OnInventoryToggled;
            UnsubscribeInventory();
        }

        void Update()
        {
            UpdateCode();
            UpdateStamina();
        }

        void OnInventoryToggled(bool open)
        {
            if (inventoryOverlay == null) return;

            inventoryOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
            {
                RebuildInventory();

                if (PlayerInventory.Local != null && subscribedInventory != PlayerInventory.Local)
                {
                    UnsubscribeInventory();
                    subscribedInventory = PlayerInventory.Local;
                    subscribedInventory.Changed += RebuildInventory;
                }
            }
        }

        void UnsubscribeInventory()
        {
            if (subscribedInventory != null)
            {
                subscribedInventory.Changed -= RebuildInventory;
                subscribedInventory = null;
            }
        }

        void RebuildInventory()
        {
            if (inventoryGrid == null) return;

            inventoryGrid.Clear();

            var inventory = PlayerInventory.Local;
            int used = inventory != null ? inventory.Items.Count : 0;
            inventoryCount.text = $"{used}/{PlayerInventory.SlotCount}";

            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("inv-slot");

                string item = inventory != null && i < inventory.Items.Count ? inventory.Items[i] : null;

                var label = new Label(item ?? "EMPTY");
                label.AddToClassList(item != null ? "inv-slot-item" : "inv-slot-empty");
                slot.Add(label);

                if (item != null)
                    slot.AddToClassList("inv-slot-filled");

                inventoryGrid.Add(slot);
            }
        }

        void UpdateCode()
        {
            if (codeResolved || codeLabel == null || Time.unscaledTime < nextCodePoll)
                return;

            nextCodePoll = Time.unscaledTime + 0.5f;

            string code = GetJoinCode();
            if (!string.IsNullOrEmpty(code))
            {
                codeLabel.text = code.ToUpperInvariant();
                codeResolved = true;
            }
        }

        void UpdateStamina()
        {
            if (staminaBar == null || staminaFill == null)
                return;

            if (localMovement == null)
            {
                var nm = NetworkManager.Singleton;
                var playerObject = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
                if (playerObject != null)
                    localMovement = playerObject.GetComponent<PlayerMovement>();

                if (localMovement == null)
                {
                    staminaBar.style.opacity = 0f;
                    return;
                }
            }

            float stamina = localMovement.Stamina01;
            staminaFill.style.width = Length.Percent(stamina * 100f);
            staminaBar.style.opacity = stamina >= 0.999f ? 0f : 1f;
        }

        static string GetJoinCode()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return null;

            var session = MultiplayerService.Instance.Sessions.Values.FirstOrDefault();
            return session?.Code;
        }
    }
}
