using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Session
{
    public class SessionHUD : MonoBehaviour
    {
        const int MenuSceneIndex = 1;

        Label codeLabel;
        VisualElement staminaBar;
        VisualElement staminaFill;
        VisualElement crosshair;

        VisualElement camOverlay;
        VisualElement recDot;
        Label camTimeLabel;
        float camTimer;

        VisualElement inventoryOverlay;
        VisualElement inventoryGrid;
        Label inventoryCount;
        VisualElement hotbar;
        VisualElement dragGhost;
        Label dragGhostLabel;

        VisualElement pauseOverlay;
        VisualElement pausePanel;
        VisualElement optionsPanel;
        Button resumeButton;
        Button optionsButton;
        Button quitButton;
        Button optionsBackButton;
        Slider sensSlider;
        Slider volumeSlider;
        Label playersCount;
        VisualElement playersList;

        // One entry per drawn slot, in both containers, for hit-testing drops
        // and tracking which slot the cursor is over.
        class SlotView
        {
            public VisualElement Element;
            public PlayerInventory.Container Container;
            public int Index;
        }

        VisualElement root;
        readonly List<SlotView> slotViews = new List<SlotView>();
        SlotView hoveredSlot;
        SlotView dragSlot;
        SlotView dropTarget;
        bool dragging;
        int dragPointerId;
        Vector2 lastPointerPos;

        PlayerMovement localMovement;
        PlayerInventory subscribedInventory;
        float nextCodePoll;
        float nextPlayersPoll;
        bool codeResolved;
        bool isQuitting;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;

            codeLabel = root.Q<Label>("CodeLabel");
            staminaBar = root.Q("StaminaBar");
            staminaFill = root.Q("StaminaFill");
            crosshair = root.Q("Crosshair");

            camOverlay = root.Q("CamOverlay");
            recDot = root.Q("RecDot");
            camTimeLabel = root.Q<Label>("CamTime");

            inventoryOverlay = root.Q("InventoryOverlay");
            inventoryGrid = root.Q("InventoryGrid");
            inventoryCount = root.Q<Label>("InventoryCount");
            hotbar = root.Q("Hotbar");
            dragGhost = root.Q("DragGhost");
            dragGhostLabel = root.Q<Label>("DragGhostLabel");

            pauseOverlay = root.Q("PauseOverlay");
            pausePanel = root.Q("PausePanel");
            optionsPanel = root.Q("OptionsPanel");
            resumeButton = root.Q<Button>("ResumeButton");
            optionsButton = root.Q<Button>("OptionsButton");
            quitButton = root.Q<Button>("QuitButton");
            optionsBackButton = root.Q<Button>("OptionsBackButton");
            sensSlider = root.Q<Slider>("SensSlider");
            volumeSlider = root.Q<Slider>("VolumeSlider");
            playersCount = root.Q<Label>("PlayersCount");
            playersList = root.Q("PlayersList");

            resumeButton.clicked += OnResumeClicked;
            optionsButton.clicked += OnOptionsClicked;
            quitButton.clicked += OnQuitClicked;
            optionsBackButton.clicked += OnOptionsBackClicked;
            sensSlider.RegisterValueChangedCallback(OnSensChanged);
            volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);

            LocalUi.InventoryToggled += OnInventoryToggled;
            LocalUi.PauseToggled += OnPauseToggled;
            Player.PlayerCam.CamcorderToggled += OnCamcorderToggled;

            // Tracked on the root so hover survives slot rebuilds and keeps
            // working over the gaps between slots.
            root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);

            AudioListener.volume = PlayerPrefs.GetFloat(GameSettings.VolumePrefKey, GameSettings.DefaultVolume);

            // Single player has no session code to show.
            if (GameSettings.SinglePlayer)
            {
                var codePanel = root.Q("CodePanel");
                if (codePanel != null)
                    codePanel.style.display = DisplayStyle.None;
                codeResolved = true;
            }

            OnInventoryToggled(LocalUi.InventoryOpen);
            OnPauseToggled(LocalUi.PauseOpen);
            OnCamcorderToggled(Player.PlayerCam.CamcorderOn);
        }

        void OnDisable()
        {
            resumeButton.clicked -= OnResumeClicked;
            optionsButton.clicked -= OnOptionsClicked;
            quitButton.clicked -= OnQuitClicked;
            optionsBackButton.clicked -= OnOptionsBackClicked;

            LocalUi.InventoryToggled -= OnInventoryToggled;
            LocalUi.PauseToggled -= OnPauseToggled;
            Player.PlayerCam.CamcorderToggled -= OnCamcorderToggled;
            root.UnregisterCallback<PointerMoveEvent>(OnRootPointerMove);
            CancelDrag();
            UnsubscribeInventory();
        }

        void Update()
        {
            HandleEscape();
            EnsureInventorySubscription();
            HandleHotbarKeys();
            UpdateCode();
            UpdateStamina();
            UpdateCamcorder();

            if (LocalUi.PauseOpen && Time.unscaledTime >= nextPlayersPoll)
            {
                nextPlayersPoll = Time.unscaledTime + 0.5f;
                RefreshPlayers();
            }
        }

        void HandleEscape()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || isQuitting)
                return;

            // Escape closes the inventory first; otherwise it toggles the menu.
            if (LocalUi.InventoryOpen)
                LocalUi.SetInventory(false);
            else
                LocalUi.SetPause(!LocalUi.PauseOpen);
        }

        // ── Camcorder ──

        void OnCamcorderToggled(bool on)
        {
            if (camOverlay == null) return;

            camOverlay.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

            // The viewfinder brackets replace the crosshair.
            if (crosshair != null)
                crosshair.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;

            if (on)
                camTimer = 0f;
        }

        void UpdateCamcorder()
        {
            if (camOverlay == null || camOverlay.style.display == DisplayStyle.None)
                return;

            camTimer += Time.deltaTime;
            var t = TimeSpan.FromSeconds(camTimer);
            camTimeLabel.text = $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

            // Classic camcorder REC blink.
            recDot.style.opacity = Time.unscaledTime % 1f < 0.6f ? 1f : 0f;
        }

        // ── Pause menu ──

        void OnPauseToggled(bool open)
        {
            if (pauseOverlay == null) return;

            pauseOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
            {
                ShowPauseMain();
                RefreshPlayers();
                nextPlayersPoll = Time.unscaledTime + 0.5f;

                sensSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameSettings.SensPrefKey, GameSettings.DefaultSens));
                volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameSettings.VolumePrefKey, GameSettings.DefaultVolume));
            }
        }

        void OnResumeClicked()
        {
            LocalUi.SetPause(false);
        }

        void OnOptionsClicked()
        {
            pausePanel.style.display = DisplayStyle.None;
            optionsPanel.style.display = DisplayStyle.Flex;
        }

        void OnOptionsBackClicked()
        {
            ShowPauseMain();
        }

        void ShowPauseMain()
        {
            pausePanel.style.display = DisplayStyle.Flex;
            optionsPanel.style.display = DisplayStyle.None;
        }

        void OnSensChanged(ChangeEvent<float> evt)
        {
            PlayerPrefs.SetFloat(GameSettings.SensPrefKey, evt.newValue);

            var cam = LocalPlayerComponent<Player.PlayerCam>();
            if (cam != null)
                cam.Sensitivity = evt.newValue;
        }

        void OnVolumeChanged(ChangeEvent<float> evt)
        {
            AudioListener.volume = evt.newValue;
            PlayerPrefs.SetFloat(GameSettings.VolumePrefKey, evt.newValue);
        }

        async void OnQuitClicked()
        {
            if (isQuitting) return;
            isQuitting = true;

            try
            {
                var session = GetSession();
                if (session != null)
                {
                    if (session.IsHost)
                        await session.AsHost().DeleteAsync();
                    else
                        await session.LeaveAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to leave session cleanly: {e}");
            }

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();

            LocalUi.Clear();
            SceneManager.LoadScene(MenuSceneIndex);
        }

        void RefreshPlayers()
        {
            if (playersList == null) return;

            var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)
                .OrderBy(p => p.OwnerClientId)
                .ToArray();

            var session = GetSession();
            playersCount.text = session != null
                ? $"{players.Length}/{session.MaxPlayers}"
                : players.Length.ToString();

            playersList.Clear();

            foreach (var player in players)
            {
                var row = new VisualElement();
                row.AddToClassList("player-row");

                var name = new Label($"PLAYER {player.OwnerClientId + 1}");
                name.AddToClassList("player-name");
                row.Add(name);

                if (player.IsOwner)
                {
                    var you = new Label("YOU");
                    you.AddToClassList("player-you");
                    row.Add(you);
                }

                playersList.Add(row);
            }
        }

        // ── Inventory & hotbar ──

        void OnInventoryToggled(bool open)
        {
            if (inventoryOverlay == null) return;

            inventoryOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            // Closing mid-drag would leave the ghost stranded on screen.
            if (!open)
                CancelDrag();

            RebuildSlots();
        }

        // The hotbar is on screen even while the backpack is closed, so the
        // subscription can't wait for the overlay to open.
        void EnsureInventorySubscription()
        {
            if (subscribedInventory == PlayerInventory.Local) return;

            UnsubscribeInventory();

            if (PlayerInventory.Local != null)
            {
                subscribedInventory = PlayerInventory.Local;
                subscribedInventory.Changed += RebuildSlots;
            }

            RebuildSlots();
        }

        void UnsubscribeInventory()
        {
            if (subscribedInventory != null)
            {
                subscribedInventory.Changed -= RebuildSlots;
                subscribedInventory = null;
            }
        }

        void RebuildSlots()
        {
            slotViews.Clear();

            var inventory = PlayerInventory.Local;

            if (inventoryGrid != null)
            {
                inventoryGrid.Clear();
                inventoryCount.text = $"{(inventory != null ? inventory.UsedSlots : 0)}/{PlayerInventory.SlotCount}";

                for (int i = 0; i < PlayerInventory.SlotCount; i++)
                    inventoryGrid.Add(CreateSlot(PlayerInventory.Container.Backpack, i, inventory));
            }

            if (hotbar != null)
            {
                hotbar.Clear();

                for (int i = 0; i < PlayerInventory.HotbarSlotCount; i++)
                    hotbar.Add(CreateSlot(PlayerInventory.Container.Hotbar, i, inventory));
            }

            // The old elements are gone; re-resolve what the cursor is over so
            // the 1-4 keys keep targeting the slot under it after a swap.
            hoveredSlot = null;
            UpdateHover(lastPointerPos);
        }

        VisualElement CreateSlot(PlayerInventory.Container container, int index, PlayerInventory inventory)
        {
            bool isHotbar = container == PlayerInventory.Container.Hotbar;
            string item = inventory != null ? inventory.GetSlot(container, index) : null;

            var slot = new VisualElement();
            slot.AddToClassList("inv-slot");
            if (isHotbar)
                slot.AddToClassList("hotbar-slot");
            if (item != null)
                slot.AddToClassList("inv-slot-filled");

            var label = new Label(item ?? (isHotbar ? string.Empty : "EMPTY"));
            label.AddToClassList(item != null ? "inv-slot-item" : "inv-slot-empty");
            label.pickingMode = PickingMode.Ignore;
            slot.Add(label);

            if (isHotbar)
            {
                var number = new Label((index + 1).ToString());
                number.AddToClassList("hotbar-number");
                number.pickingMode = PickingMode.Ignore;
                slot.Add(number);
            }

            var view = new SlotView { Element = slot, Container = container, Index = index };
            slotViews.Add(view);

            slot.RegisterCallback<PointerDownEvent>(evt => BeginDrag(evt, view));
            slot.RegisterCallback<PointerMoveEvent>(OnDragMove);
            slot.RegisterCallback<PointerUpEvent>(OnDragEnd);
            slot.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());

            return slot;
        }

        // Swaps the hovered backpack slot into a hotbar slot with 1-4.
        void HandleHotbarKeys()
        {
            if (!LocalUi.InventoryOpen || LocalUi.PauseOpen || dragging)
                return;

            var keyboard = Keyboard.current;
            var inventory = PlayerInventory.Local;
            if (keyboard == null || inventory == null)
                return;

            if (hoveredSlot == null || hoveredSlot.Container != PlayerInventory.Container.Backpack)
                return;

            for (int i = 0; i < PlayerInventory.HotbarSlotCount; i++)
            {
                var key = keyboard[Key.Digit1 + i];
                if (key == null || !key.wasPressedThisFrame)
                    continue;

                inventory.Swap(PlayerInventory.Container.Backpack, hoveredSlot.Index,
                               PlayerInventory.Container.Hotbar, i);
                break;
            }
        }

        // ── Slot hover & drag ──

        void OnRootPointerMove(PointerMoveEvent evt)
        {
            lastPointerPos = evt.position;

            // While dragging, the drop-target highlight stands in for hover.
            if (!dragging)
                UpdateHover(lastPointerPos);
        }

        void UpdateHover(Vector2 panelPos)
        {
            SlotView view = SlotAt(panelPos);
            if (view == hoveredSlot) return;

            hoveredSlot?.Element.RemoveFromClassList("inv-slot-hover");
            hoveredSlot = view;
            hoveredSlot?.Element.AddToClassList("inv-slot-hover");
        }

        void ClearHover()
        {
            hoveredSlot?.Element.RemoveFromClassList("inv-slot-hover");
            hoveredSlot = null;
        }

        SlotView SlotAt(Vector2 panelPos)
        {
            foreach (var view in slotViews)
            {
                // Backpack slots are laid out but hidden while the overlay is closed.
                if (view.Container == PlayerInventory.Container.Backpack && !LocalUi.InventoryOpen)
                    continue;

                if (view.Element.panel != null && view.Element.worldBound.Contains(panelPos))
                    return view;
            }

            return null;
        }

        void BeginDrag(PointerDownEvent evt, SlotView view)
        {
            if (dragging || evt.button != 0 || dragGhost == null)
                return;

            var inventory = PlayerInventory.Local;
            string item = inventory != null ? inventory.GetSlot(view.Container, view.Index) : null;
            if (item == null)
                return;

            dragging = true;
            dragPointerId = evt.pointerId;
            dragSlot = view;

            dragGhostLabel.text = item;
            dragGhost.style.display = DisplayStyle.Flex;
            MoveGhost(evt.position);

            ClearHover();
            view.Element.AddToClassList("inv-slot-dragging");
            view.Element.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnDragMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != dragPointerId)
                return;

            lastPointerPos = evt.position;
            MoveGhost(evt.position);
            SetDropTarget(SlotAt(evt.position));
            evt.StopPropagation();
        }

        void OnDragEnd(PointerUpEvent evt)
        {
            if (!dragging || evt.pointerId != dragPointerId)
                return;

            SlotView source = dragSlot;
            SlotView target = SlotAt(evt.position);

            EndDrag();

            // Release before applying: the swap rebuilds every slot element,
            // destroying the one holding the pointer capture.
            if (source.Element.HasPointerCapture(evt.pointerId))
                source.Element.ReleasePointer(evt.pointerId);

            evt.StopPropagation();

            if (target != null && target != source)
            {
                PlayerInventory.Local?.Swap(source.Container, source.Index,
                                            target.Container, target.Index);
            }
        }

        void CancelDrag()
        {
            if (!dragging) return;

            SlotView source = dragSlot;
            EndDrag();

            if (source.Element.HasPointerCapture(dragPointerId))
                source.Element.ReleasePointer(dragPointerId);
        }

        void EndDrag()
        {
            dragging = false;

            dragSlot?.Element.RemoveFromClassList("inv-slot-dragging");
            dragSlot = null;

            SetDropTarget(null);

            if (dragGhost != null)
                dragGhost.style.display = DisplayStyle.None;

            UpdateHover(lastPointerPos);
        }

        void SetDropTarget(SlotView view)
        {
            if (view == dropTarget) return;

            dropTarget?.Element.RemoveFromClassList("inv-slot-drop-target");
            dropTarget = view;
            dropTarget?.Element.AddToClassList("inv-slot-drop-target");
        }

        void MoveGhost(Vector2 panelPos)
        {
            // Half of .drag-ghost's size; it is hidden until a drag starts, so
            // its resolved size isn't available on the first frame.
            const float halfSize = 34f;

            Vector2 local = dragGhost.parent.WorldToLocal(panelPos);
            dragGhost.style.left = local.x - halfSize;
            dragGhost.style.top = local.y - halfSize;
        }

        // ── HUD ──

        void UpdateCode()
        {
            if (codeResolved || codeLabel == null || Time.unscaledTime < nextCodePoll)
                return;

            nextCodePoll = Time.unscaledTime + 0.5f;

            string code = GetSession()?.Code;
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
                localMovement = LocalPlayerComponent<PlayerMovement>();
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

        static T LocalPlayerComponent<T>() where T : Component
        {
            var nm = NetworkManager.Singleton;
            var playerObject = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            return playerObject != null ? playerObject.GetComponent<T>() : null;
        }

        static ISession GetSession()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return null;

            return MultiplayerService.Instance.Sessions.Values.FirstOrDefault();
        }
    }
}
